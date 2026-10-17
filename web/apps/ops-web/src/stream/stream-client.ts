import { fetchEventSource } from "@microsoft/fetch-event-source";

// ONE constant for the stream's address. The public stream is used for now; the ops stream (`/api/v1/ops/stream`,
// Controller and StationManager only) replaces it later by changing this line.
export const STREAM_PATH = "/api/v1/stream";

// Where the connection stands, for the page to show (in words and an icon, never colour alone).
export type StreamConnection = "connecting" | "live" | "reconnecting" | "failed" | "closed";

// One Server-Sent Event as it arrived: `event:`, `id:` and `data:` of the frame (contract section 4). The data is still text.
export interface StreamFrame {
  name: string;
  id: string;
  data: string;
}

export interface StreamOptions {
  // The stream REQUIRES a filter; the ops page asks for the Main Line (`lineIds`).
  lineIds: string[];
  // Called before EVERY (re)connect, so a long-lived page never uses an expired token. Null = no token (the public stream does not need one).
  getToken: () => Promise<string | null>;
  signal: AbortSignal;
  onFrame: (frame: StreamFrame) => void;
  onConnection: (state: StreamConnection, detail?: string) => void;
  // For tests.
  fetch?: typeof globalThis.fetch;
  retryDelayMs?: (attempt: number) => number;
}

class RetryableError extends Error {}
class UnauthorizedError extends Error {}
class FatalError extends Error {}

// 1 s, 2 s, 3 s … never more than 5 s: a control room wants the picture back quickly, and one stream is cheap to reconnect.
const defaultDelay = (attempt: number) => Math.min(attempt, 5) * 1000;

// Reads the event stream with fetch, NOT the browser's EventSource: EventSource cannot send an Authorization header, and
// putting a token in the URL would leak it into logs (contract section 2, C-6). The library reconnects by itself, sends
// `Last-Event-ID` so the server only replays what was missed, and we say what happened to the connection.
export async function connectStream(options: StreamOptions): Promise<void> {
  const { lineIds, getToken, signal, onFrame, onConnection } = options;
  const baseFetch = options.fetch ?? ((input: RequestInfo | URL, init?: RequestInit) => globalThis.fetch(input, init));
  const retryDelay = options.retryDelayMs ?? defaultDelay;

  let attempt = 0;
  let unauthorizedInARow = 0;
  const url = `${STREAM_PATH}?lineIds=${lineIds.map(encodeURIComponent).join(",")}`;

  onConnection("connecting");
  try {
    await fetchEventSource(url, {
      signal,
      openWhenHidden: true,   // a control-room screen in a background tab must keep receiving (the library pauses hidden tabs by default)

      // Every (re)connect goes through here, so the Authorization header always carries a current token.
      fetch: async (input, init) => {
        const token = await getToken();
        const headers: Record<string, string> = { ...(init?.headers as Record<string, string> | undefined) };
        if (token) headers.authorization = `Bearer ${token}`;
        return baseFetch(input, { ...init, headers });
      },

      async onopen(response) {
        if (response.ok && response.headers.get("content-type")?.startsWith("text/event-stream")) {
          attempt = 0;
          unauthorizedInARow = 0;
          onConnection("live");
          return;
        }
        if (response.status === 401) throw new UnauthorizedError();
        // A request the server refuses (for example 400, no filter) will be refused again: retrying cannot help.
        if (response.status >= 400 && response.status < 500 && response.status !== 408 && response.status !== 429) {
          throw new FatalError(`The server refused the request (${response.status}).`);
        }
        throw new RetryableError();
      },

      onmessage(message) {
        // The server sends a comment line (": keep-alive") every 15 s so proxies keep the connection open. This library
        // version reports it as an EMPTY message instead of skipping it; it is not an event, so it is dropped here.
        if (!message.event && !message.data && !message.id) return;
        onFrame({ name: message.event, id: message.id, data: message.data });
      },

      // The server ended the stream. A stream is meant to stay open, so this is not "finished": reconnect.
      onclose() {
        throw new RetryableError();
      },

      onerror(error) {
        if (error instanceof FatalError) throw error;   // returning would retry; throwing stops
        if (error instanceof UnauthorizedError && ++unauthorizedInARow >= 2) {
          // The first 401 may only mean the token had just expired, and the next attempt asks for a fresh one.
          throw new FatalError("The server did not accept your credentials. Please sign in again.");
        }
        attempt += 1;
        onConnection("reconnecting");
        return retryDelay(attempt);
      },
    });
  } catch (error) {
    onConnection("failed", error instanceof FatalError ? error.message : "The live connection stopped unexpectedly.");
    return;
  }

  onConnection("closed");   // the page left (the signal was aborted)
}
