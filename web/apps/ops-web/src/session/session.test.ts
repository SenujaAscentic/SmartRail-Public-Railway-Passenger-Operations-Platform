import { beforeEach, describe, expect, it } from "vitest";
import { createApiClient } from "@smartrail/api-client";
import { Session } from "./session.ts";

// ---- a fake server and a fake clock ----

const SIGN_IN = "POST /api/v1/auth/staff/sign-in";
const REFRESH = "POST /api/v1/auth/refresh";
const SIGN_OUT = "POST /api/v1/auth/sign-out";
const MINUTE = 60_000;

const json = (status: number, body: unknown, headers: Record<string, string> = {}) =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json", ...headers } });

let clock: number;
let calls: string[];
let routes: Record<string, () => Response | Promise<Response>>;

const accessToken = (name: string) => json(200, { accessToken: name, expiresAt: new Date(clock + 15 * MINUTE).toISOString() });
const refused = () => json(401, { code: "UNAUTHENTICATED", title: "Sign in required", status: 401, requestId: "r1" });

function newSession(): Session {
  const fetch = async (request: Request): Promise<Response> => {
    const key = `${request.method} ${new URL(request.url).pathname}`;
    calls.push(key);
    const route = routes[key];
    if (!route) throw new Error(`The test did not expect a call to ${key}`);
    return route();
  };
  return new Session({ api: createApiClient({ baseUrl: "http://localhost", fetch }), now: () => clock });
}

// Lets the pending promises (fetch, the refresh) finish before the test looks at the calls.
const flush = () => new Promise<void>((resolve) => setTimeout(resolve, 0));

const count = (key: string) => calls.filter((c) => c === key).length;

beforeEach(() => {
  clock = Date.parse("2026-10-16T05:00:00Z");
  calls = [];
  routes = { [SIGN_IN]: () => accessToken("token-1"), [REFRESH]: () => accessToken("token-2"), [SIGN_OUT]: () => new Response(null, { status: 204 }) };
  localStorage.clear();
  sessionStorage.clear();
});

describe("signing in", () => {
  it("signs in, keeps the access token in memory only, and hands it out while it is valid", async () => {
    const session = newSession();

    expect(await session.signIn("CTRL-01", "pw")).toEqual({ kind: "ok" });

    expect(session.getSnapshot()).toEqual({ status: "signedIn", endedReason: null });
    expect(await session.getAccessToken()).toBe("token-1");
    expect(count(REFRESH)).toBe(0);
    // Memory only (threat T-14): nothing a script on another page could read.
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
    expect(document.cookie).toBe("");
  });

  it("sends the staff number and password as the contract says", async () => {
    let body: unknown;
    const fetch = async (request: Request) => {
      body = await request.json();
      return accessToken("t");
    };
    const session = new Session({ api: createApiClient({ baseUrl: "http://localhost", fetch }), now: () => clock });

    await session.signIn("SM-FOT", "secret");

    expect(body).toEqual({ staffNumber: "SM-FOT", password: "secret" });
  });

  it("answers 'invalid' for the generic 401 and stays signed out", async () => {
    routes[SIGN_IN] = () => json(401, { code: "UNAUTHENTICATED", title: "Sign-in failed", status: 401, requestId: "r" });
    const session = newSession();

    expect(await session.signIn("CTRL-01", "wrong")).toEqual({ kind: "invalid" });

    expect(session.getSnapshot().status).not.toBe("signedIn");   // (still "checking": restore() has not run in this test)
    expect(await session.getAccessToken()).toBeNull();
  });

  it("answers 'locked' with the wait in minutes for 429 SIGN_IN_LOCKED", async () => {
    routes[SIGN_IN] = () => json(429, { code: "SIGN_IN_LOCKED", title: "Sign-in is locked", status: 429, requestId: "r" }, { "retry-after": "840" });
    const session = newSession();

    expect(await session.signIn("SM-KDT", "x")).toEqual({ kind: "locked", retryAfterMinutes: 14 });
  });

  it("rounds the wait up and never says 0 minutes", async () => {
    routes[SIGN_IN] = () => json(429, { code: "SIGN_IN_LOCKED", title: "locked", status: 429, requestId: "r" }, { "retry-after": "1" });
    const session = newSession();

    expect(await session.signIn("SM-KDT", "x")).toEqual({ kind: "locked", retryAfterMinutes: 1 });
  });

  it("does not call a 429 with another code a lock", async () => {
    routes[SIGN_IN] = () => json(429, { code: "RATE_LIMITED", title: "slow down", status: 429, requestId: "r" });
    const session = newSession();

    expect(await session.signIn("CTRL-01", "x")).toEqual({ kind: "server" });
  });

  it("passes on the field messages of a 400", async () => {
    routes[SIGN_IN] = () =>
      json(400, { code: "VALIDATION_FAILED", title: "The request has errors", status: 400, requestId: "r", errors: { staffNumber: ["Enter your staff number."] } });
    const session = newSession();

    expect(await session.signIn("", "x")).toEqual({ kind: "validation", errors: { staffNumber: ["Enter your staff number."] } });
  });

  it("answers 'network' when the server cannot be reached, and 'server' for a 500", async () => {
    routes[SIGN_IN] = () => {
      throw new TypeError("Failed to fetch");
    };
    expect(await newSession().signIn("CTRL-01", "x")).toEqual({ kind: "network" });

    routes[SIGN_IN] = () => json(500, { code: "ERROR", title: "x", status: 500, requestId: "r" });
    expect(await newSession().signIn("CTRL-01", "x")).toEqual({ kind: "server" });
  });
});

describe("keeping the access token fresh", () => {
  it("refreshes through the cookie when the token is about to expire and the user is active", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 14 * MINUTE + 40_000;   // 20 s left: inside the 30 s safety margin
    session.noteActivity();

    expect(await session.getAccessToken()).toBe("token-2");

    expect(count(REFRESH)).toBe(1);
    expect(session.getSnapshot().status).toBe("signedIn");
  });

  it("makes ONE refresh when several requests need a token at the same moment", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 16 * MINUTE;
    session.noteActivity();

    const tokens = await Promise.all([session.getAccessToken(), session.getAccessToken(), session.getAccessToken()]);

    expect(tokens).toEqual(["token-2", "token-2", "token-2"]);
    expect(count(REFRESH)).toBe(1);   // the server would otherwise see a reused refresh token (threat T-14)
  });

  it("ends the session with the 'expired' message when the server refuses the refresh", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 20 * MINUTE;
    routes[REFRESH] = refused;   // set BEFORE the activity: noteActivity() starts a refresh by itself when the last one is old
    session.noteActivity();

    expect(await session.getAccessToken()).toBeNull();

    expect(session.getSnapshot()).toEqual({ status: "signedOut", endedReason: "expired" });
  });

  it("does NOT refresh for someone who has been away for more than 30 minutes: it ends the session as 'idle'", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 31 * MINUTE;   // no activity at all since sign-in

    expect(await session.getAccessToken()).toBeNull();

    expect(count(REFRESH)).toBe(0);
    expect(session.getSnapshot()).toEqual({ status: "signedOut", endedReason: "idle" });
    expect(count(SIGN_OUT)).toBe(1);   // the refresh cookie is revoked too, not just forgotten
  });

  it("keeps a session that was active within the last 30 minutes", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 25 * MINUTE;
    session.noteActivity();
    clock += 5 * MINUTE;   // 30 minutes since sign-in but only 5 since the last activity

    expect(await session.getAccessToken()).toBe("token-2");
  });

  it("stays signed in when the refresh cannot reach the server, and tries again later", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 16 * MINUTE;
    routes[REFRESH] = () => {
      throw new TypeError("Failed to fetch");
    };
    session.noteActivity();

    expect(await session.getAccessToken()).toBeNull();
    expect(session.getSnapshot().status).toBe("signedIn");   // a dropped connection is not a sign-out

    routes[REFRESH] = () => accessToken("token-3");
    expect(await session.getAccessToken()).toBe("token-3");
  });
});

describe("a server error on refresh", () => {
  it("is not a sign-out: only a 401 says the session is over", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 16 * MINUTE;
    routes[REFRESH] = () => json(500, { code: "ERROR", title: "x", status: 500, requestId: "r" });
    session.noteActivity();

    expect(await session.getAccessToken()).toBeNull();

    expect(session.getSnapshot()).toEqual({ status: "signedIn", endedReason: null });
  });
});

describe("activity", () => {
  it("refreshes in the background when the user is active and the last refresh is over 10 minutes old", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 11 * MINUTE;

    session.noteActivity();
    await flush();

    expect(count(REFRESH)).toBe(1);
  });

  it("does not refresh for activity soon after the last refresh", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 5 * MINUTE;

    session.noteActivity();

    expect(count(REFRESH)).toBe(0);
  });

  it("does nothing while signed out", () => {
    const session = newSession();

    session.noteActivity();

    expect(calls).toEqual([]);
  });

  it("does not start a second refresh while one is running", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 11 * MINUTE;

    session.noteActivity();
    session.noteActivity();
    session.noteActivity();
    await flush();

    expect(count(REFRESH)).toBe(1);
  });
});

describe("restoring a session after a page load", () => {
  it("signs in again from the refresh cookie, with no message", async () => {
    const session = newSession();
    expect(session.getSnapshot().status).toBe("checking");

    await session.restore();

    expect(session.getSnapshot()).toEqual({ status: "signedIn", endedReason: null });
    expect(await session.getAccessToken()).toBe("token-2");
  });

  it("goes to signed out WITHOUT a message when there is no valid cookie (nobody was signed in)", async () => {
    routes[REFRESH] = refused;
    const session = newSession();

    await session.restore();

    expect(session.getSnapshot()).toEqual({ status: "signedOut", endedReason: null });
  });

  it("does nothing when the session already ended, so its message is not wiped", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    clock += 31 * MINUTE;
    await session.getAccessToken();   // ends as idle
    calls.length = 0;

    await session.restore();

    expect(session.getSnapshot()).toEqual({ status: "signedOut", endedReason: "idle" });
    expect(calls).toEqual([]);
  });

  it("goes to signed out when the server cannot be reached", async () => {
    routes[REFRESH] = () => {
      throw new TypeError("Failed to fetch");
    };
    const session = newSession();

    await session.restore();

    expect(session.getSnapshot().status).toBe("signedOut");
  });
});

describe("signing out", () => {
  it("revokes the session on the server, forgets the token and says nothing", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");

    await session.signOut();

    expect(count(SIGN_OUT)).toBe(1);
    expect(session.getSnapshot()).toEqual({ status: "signedOut", endedReason: null });
    expect(await session.getAccessToken()).toBeNull();
  });

  it("signs out here even when the server cannot be reached", async () => {
    const session = newSession();
    await session.signIn("CTRL-01", "pw");
    routes[SIGN_OUT] = () => {
      throw new TypeError("Failed to fetch");
    };

    await session.signOut();

    expect(session.getSnapshot().status).toBe("signedOut");
  });

  it("tells subscribers about every change", async () => {
    const session = newSession();
    const seen: string[] = [];
    session.subscribe(() => seen.push(session.getSnapshot().status));

    await session.signIn("CTRL-01", "pw");
    await session.signOut();

    expect(seen).toEqual(["signedIn", "signedOut"]);
  });
});
