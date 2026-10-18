import type { ApiClient } from "@smartrail/api-client";

// ---- what the rest of the app sees ----

// "checking": the page just loaded and we are asking the server whether the refresh cookie still works.
export interface SessionState {
  status: "checking" | "signedOut" | "signedIn";
  // Why the session ended on its own (shown on the sign-in page). Null when the person signed out, or never signed in.
  endedReason: EndedReason | null;
}
export type EndedReason = "idle" | "expired";

export type SignInResult =
  | { kind: "ok" }
  | { kind: "invalid" }                                        // wrong staff number or password: ONE message, never which
  | { kind: "locked"; retryAfterMinutes: number }              // 429 SIGN_IN_LOCKED
  | { kind: "validation"; errors: Record<string, string[]> }   // 400 with field messages
  | { kind: "network" }                                        // the server could not be reached
  | { kind: "server" };                                        // anything else

// ---- the rules (all times in milliseconds) ----

// US-O1: the session ends after 30 minutes without activity.
export const IDLE_LIMIT_MS = 30 * 60_000;
// A token that expires within this time is treated as expired, so a request never leaves with a token that dies in flight.
const SAFETY_MARGIN_MS = 30_000;
// While the person is active, keep the server's idle clock alive by refreshing once the last refresh is this old.
// (The server ends a session whose refresh token is 30 minutes old; reading a screen without clicking must not end it.)
const REFRESH_ON_ACTIVITY_AFTER_MS = 10 * 60_000;

type RefreshOutcome = "ok" | "refused" | "unreachable";

// One browser session of one staff member. It holds the ACCESS token in memory only (nothing in localStorage,
// sessionStorage or a cookie that a script could read: threat T-14) and gets a new one through the HttpOnly refresh
// cookie, which the browser sends by itself (contract section 2).
//
// There is NO timer. A new access token is fetched only when something needs one, and only for a person who has been
// active in the last 30 minutes. An unattended screen therefore stops refreshing, and the server's own refusal
// (and this class's idle check) end the session.
export class Session {
  readonly #api: ApiClient;
  readonly #now: () => number;

  #state: SessionState = { status: "checking", endedReason: null };
  #token: string | null = null;
  #expiresAt = 0;
  #lastRefreshAt = 0;
  #lastActivityAt: number;
  #refreshing: Promise<RefreshOutcome> | null = null;
  readonly #listeners = new Set<() => void>();

  // `now` is a parameter so tests can move time; the app uses the real clock.
  constructor(deps: { api: ApiClient; now?: () => number }) {
    this.#api = deps.api;
    this.#now = deps.now ?? Date.now;
    this.#lastActivityAt = this.#now();
  }

  // For React's useSyncExternalStore: the snapshot object is replaced whenever the state changes.
  readonly getSnapshot = (): SessionState => this.#state;

  readonly subscribe = (listener: () => void): (() => void) => {
    this.#listeners.add(listener);
    return () => this.#listeners.delete(listener);
  };

  // On page load: the access token is gone (memory), but the cookie may still be good.
  async restore(): Promise<void> {
    // Only once, at the start. Later the state belongs to what the person did, and a message about why the session ended must survive.
    if (this.#state.status !== "checking") return;
    const outcome = await this.#refresh();
    if (this.#state.status !== "checking") return;   // the person signed in while we were asking
    // No message when it fails: nobody was signed in, or the page cannot reach the server.
    this.#set(outcome === "ok" ? { status: "signedIn", endedReason: null } : { status: "signedOut", endedReason: null });
  }

  async signIn(staffNumber: string, password: string): Promise<SignInResult> {
    let result;
    try {
      result = await this.#api.POST("/api/v1/auth/staff/sign-in", { body: { staffNumber, password } });
    } catch {
      return { kind: "network" };
    }

    const { data, error, response } = result;
    if (data) {
      this.#accept(data.accessToken, data.expiresAt);
      this.#lastActivityAt = this.#now();
      this.#set({ status: "signedIn", endedReason: null });
      return { kind: "ok" };
    }
    if (response.status === 401) return { kind: "invalid" };
    if (response.status === 429 && error?.code === "SIGN_IN_LOCKED") {
      const seconds = Number(response.headers.get("retry-after")) || 15 * 60;   // the lock lasts 15 minutes
      return { kind: "locked", retryAfterMinutes: Math.max(1, Math.ceil(seconds / 60)) };
    }
    if (response.status === 400 && error?.errors) return { kind: "validation", errors: error.errors };
    return { kind: "server" };
  }

  async signOut(): Promise<void> {
    await this.#revoke();
    this.#token = null;
    this.#set({ status: "signedOut", endedReason: null });
  }

  // Called for pointer, key and scroll events. Remembers that the person is here, and if their last refresh is getting old,
  // refreshes now (not on a timer) so the server does not count them as idle while they read the screen.
  noteActivity(): void {
    const now = this.#now();
    this.#lastActivityAt = now;
    if (this.#state.status === "signedIn" && !this.#refreshing && now - this.#lastRefreshAt >= REFRESH_ON_ACTIVITY_AFTER_MS) {
      void this.#refreshForActivity();
    }
  }

  // The access token for one request, or null (signed out, session over, or the server cannot be reached).
  async getAccessToken(): Promise<string | null> {
    if (this.#state.status !== "signedIn") return null;
    if (this.#token && this.#expiresAt - this.#now() > SAFETY_MARGIN_MS) return this.#token;

    // A new token is needed. Not for someone who has been away: they sign in again.
    if (this.#now() - this.#lastActivityAt > IDLE_LIMIT_MS) {
      await this.#end("idle", { revoke: true });
      return null;
    }

    const outcome = await this.#refresh();
    if (outcome === "refused") {
      await this.#end("expired");
      return null;
    }
    return outcome === "ok" ? this.#token : null;
  }

  // ---- inside ----

  async #refreshForActivity(): Promise<void> {
    if ((await this.#refresh()) === "refused") {
      await this.#end("expired");
    }
  }

  // At most one refresh at a time: the server lets a refresh token be used once (plus a short grace), so
  // several requests that all need a token must share ONE refresh.
  #refresh(): Promise<RefreshOutcome> {
    this.#refreshing ??= this.#callRefresh().finally(() => {
      this.#refreshing = null;
    });
    return this.#refreshing;
  }

  async #callRefresh(): Promise<RefreshOutcome> {
    try {
      const { data, response } = await this.#api.POST("/api/v1/auth/refresh");
      if (data) {
        this.#accept(data.accessToken, data.expiresAt);
        return "ok";
      }
      // 401 means the server will not refresh this session (idle too long, signed out elsewhere, account switched off).
      // Anything else (a 500) says nothing about the session, so it is kept.
      return response.status === 401 ? "refused" : "unreachable";
    } catch {
      return "unreachable";
    }
  }

  #accept(accessToken: string, expiresAt: string): void {
    this.#token = accessToken;
    this.#expiresAt = Date.parse(expiresAt);
    this.#lastRefreshAt = this.#now();
  }

  async #end(reason: EndedReason, options: { revoke?: boolean } = {}): Promise<void> {
    if (this.#state.status !== "signedIn") return;   // several callers can notice at once; the first one wins
    this.#token = null;
    this.#set({ status: "signedOut", endedReason: reason });
    if (options.revoke) await this.#revoke();
  }

  // Best effort: tell the server to forget the refresh token. If it cannot be reached the cookie simply expires.
  async #revoke(): Promise<void> {
    try {
      await this.#api.POST("/api/v1/auth/sign-out");
    } catch {
      // nothing to do
    }
  }

  #set(state: SessionState): void {
    this.#state = state;
    for (const listener of this.#listeners) listener();
  }
}
