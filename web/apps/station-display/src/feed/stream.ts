import { fetchEventSource } from "@microsoft/fetch-event-source";

// The public event stream, filtered to one station (contract section 4): `GET /api/v1/stream?stationIds=<id>`.
export const STREAM_PATH = "/api/v1/stream";

// The server sends a heartbeat comment every 15 s. No bytes at all for three of them means the connection is dead (a
// cable pulled, a proxy that swallowed it): the display closes it and connects again instead of waiting for ever.
export const SILENCE_MS = 45_000;

// 1 s, 2 s, 3 s ... never more than 5 s: a display wants the picture back quickly, and one stream is cheap to reconnect.
const defaultRetryDelay = (attempt: number) => Math.min(attempt, 5) * 1000;

// One Server-Sent Event as it arrived (the data is still text).
export interface StreamFrame {
  name: string;
  id: string;
  data: string;
}

export interface StationStreamOptions {
  stationId: string;
  // Aborted when the page goes away: the loop ends.
  signal: AbortSignal;
  // A connection is established (the server answered with an event stream).
  onOpen: () => void;
  // Any bytes arrived: an event OR a heartbeat. This is what keeps the display "in contact".
  onActivity: () => void;
  // A real event (not a heartbeat).
  onFrame: (frame: StreamFrame) => void;
  // The connection ended or failed; the loop waits and connects again.
  onClosed: () => void;
  // For tests.
  fetch?: typeof globalThis.fetch;
  retryDelayMs?: (attempt: number) => number;
  silenceMs?: number;
}

class Retry extends Error {}

// Waits [ms], or less if the page leaves meanwhile.
function sleep(ms: number, signal: AbortSignal): Promise<void> {
  return new Promise((resolve) => {
    if (signal.aborted) return resolve();
    const timer = setTimeout(done, ms);
    function done() {
      clearTimeout(timer);
      signal.removeEventListener("abort", done);
      resolve();
    }
    signal.addEventListener("abort", done);
  });
}

// Reads the stream with fetch and reconnects by itself: after an error, after the server ends it, and after silence. It sends
// `Last-Event-ID`, so the server replays what was missed (or answers `resync`; the feed reloads the board after every
// (re)connect anyway). The stream is public, so no token is sent.
export async function connectStationStream(options: StationStreamOptions): Promise<void> {
  const { stationId, signal } = options;
  const baseFetch = options.fetch ?? ((input: RequestInfo | URL, init?: RequestInit) => globalThis.fetch(input, init));
  const retryDelay = options.retryDelayMs ?? defaultRetryDelay;
  const silenceMs = options.silenceMs ?? SILENCE_MS;
  const url = `${STREAM_PATH}?stationIds=${encodeURIComponent(stationId)}`;

  let attempt = 0;
  let lastEventId = "";

  while (!signal.aborted) {
    // One controller per connection, so that silence can close THIS connection and not the whole loop.
    const connection = new AbortController();
    const abortConnection = () => connection.abort();
    signal.addEventListener("abort", abortConnection);

    let silence: ReturnType<typeof setTimeout> | undefined;
    const armSilence = () => {
      clearTimeout(silence);
      silence = setTimeout(abortConnection, silenceMs);
    };

    try {
      await fetchEventSource(url, {
        signal: connection.signal,
        openWhenHidden: true, // a display in a background tab must keep receiving (the library pauses hidden tabs by default)
        fetch: (input, init) => {
          const headers: Record<string, string> = { ...(init?.headers as Record<string, string> | undefined) };
          if (lastEventId) headers["Last-Event-ID"] = lastEventId;
          return baseFetch(input, { ...init, headers });
        },
        async onopen(response) {
          if (!response.ok || !response.headers.get("content-type")?.startsWith("text/event-stream")) throw new Retry();
          attempt = 0;
          armSilence();
          options.onActivity();
          options.onOpen();
        },
        onmessage(message) {
          armSilence();
          options.onActivity();
          // The heartbeat comment line is reported by this library version as an EMPTY message: not an event.
          if (!message.event && !message.data && !message.id) return;
          if (message.id) lastEventId = message.id;
          options.onFrame({ name: message.event, id: message.id, data: message.data });
        },
        // The server ended the stream. A stream is meant to stay open, so this is not "finished": connect again.
        onclose() {
          throw new Retry();
        },
        // Throwing (not returning) stops the library's own retry: this loop does the retrying.
        onerror(error) {
          throw error;
        },
      });
    } catch {
      // Any failure, including silence (we aborted the connection ourselves): fall through to the pause and the next try.
    } finally {
      clearTimeout(silence);
      signal.removeEventListener("abort", abortConnection);
    }

    if (signal.aborted) return;
    options.onClosed();
    attempt += 1;
    await sleep(retryDelay(attempt), signal);
  }
}
