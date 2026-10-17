import { describe, expect, it } from "vitest";
import { frame, sse, type Responder } from "../test/sse.ts";
import { connectStream, STREAM_PATH, type StreamConnection, type StreamFrame } from "./stream-client.ts";

const LINE = "5d1c8e0a-2b47-4f6a-9c33-71a0e4d8b2f6";
const status = (code: number): Responder => () => new Response(JSON.stringify({ code: "X", title: "x", status: code }), { status: code, headers: { "content-type": "application/problem+json" } });

// Answers the n-th request with the n-th responder (the last one repeats).
function harness(responders: Responder[], tokens: (string | null)[] = ["token-1"]) {
  const requests: { url: string; headers: Record<string, string> }[] = [];
  let call = 0;
  let tokenCall = 0;
  const fetch = async (input: RequestInfo | URL, init: RequestInit = {}) => {
    requests.push({ url: String(input), headers: { ...(init.headers as Record<string, string>) } });   // copy: the library edits its headers later
    return responders[Math.min(call++, responders.length - 1)]!(init);
  };

  const states: StreamConnection[] = [];
  const details: (string | undefined)[] = [];
  const frames: StreamFrame[] = [];
  const controller = new AbortController();
  const done = connectStream({
    lineIds: [LINE],
    getToken: async () => tokens[Math.min(tokenCall++, tokens.length - 1)] ?? null,
    fetch,
    signal: controller.signal,
    onFrame: (f) => frames.push(f),
    onConnection: (state, detail) => {
      states.push(state);
      details.push(detail);
    },
    retryDelayMs: () => 1,   // real timers, but no real waiting
  });
  return { requests, states, details, frames, controller, done, tokenCalls: () => tokenCall };
}

const until = async (condition: () => boolean) => {
  for (let i = 0; i < 400 && !condition(); i++) await new Promise((resolve) => setTimeout(resolve, 5));
  expect(condition()).toBe(true);
};

describe("the request", () => {
  it("asks for the line's events as an event stream, with the token in a HEADER and nothing secret in the URL", async () => {
    const { requests, controller, done } = harness([sse("", { hold: true })]);
    await until(() => requests.length === 1);

    expect(new URL(requests[0]!.url, "http://x").pathname).toBe(STREAM_PATH);
    expect(new URL(requests[0]!.url, "http://x").searchParams.get("lineIds")).toBe(LINE);
    expect(requests[0]!.headers.accept).toBe("text/event-stream");
    expect(requests[0]!.headers.authorization).toBe("Bearer token-1");
    expect(requests[0]!.url).not.toContain("token-1");   // tokens in URLs leak into logs (contract section 2)
    controller.abort();
    await done;
  });

  it("sends no Authorization header when there is no token (the public stream does not need one)", async () => {
    const { requests, controller, done } = harness([sse("", { hold: true })], [null]);
    await until(() => requests.length === 1);

    expect(requests[0]!.headers.authorization).toBeUndefined();
    controller.abort();
    await done;
  });

  it("joins several line ids with commas", async () => {
    const requests: string[] = [];
    const controller = new AbortController();
    const done = connectStream({
      lineIds: [LINE, "0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01"],
      getToken: async () => null,
      fetch: async (input, init) => {
        requests.push(String(input));
        return sse("", { hold: true })(init ?? {});
      },
      signal: controller.signal,
      onFrame: () => {},
      onConnection: () => {},
    });
    await until(() => requests.length === 1);

    expect(new URL(requests[0]!, "http://x").searchParams.get("lineIds")).toBe(`${LINE},0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01`);
    controller.abort();
    await done;
  });
});

describe("the frames", () => {
  it("hands over each frame with its event name, id and data, and ignores heartbeat comments", async () => {
    const text = `: keep-alive\n\n${frame("snapshot", { journeyId: "j1", sequence: 1051 })}${frame("DelayChanged", { sequence: 1052, type: "DelayChanged" }, 1052)}`;
    const { frames, controller, done } = harness([sse(text, { hold: true })]);
    await until(() => frames.length === 2);

    expect(frames.map((f) => f.name)).toEqual(["snapshot", "DelayChanged"]);
    expect(frames[1]).toMatchObject({ id: "1052" });
    expect(JSON.parse(frames[1]!.data)).toEqual({ sequence: 1052, type: "DelayChanged" });
    controller.abort();
    await done;
  });
});

describe("the connection", () => {
  it("is 'connecting' first and 'live' once the stream opens, and 'closed' after the page leaves", async () => {
    const { states, controller, done } = harness([sse("", { hold: true })]);
    await until(() => states.includes("live"));

    controller.abort();
    await done;

    expect(states).toEqual(["connecting", "live", "closed"]);
  });

  it("reconnects when the server ends the stream, and resumes from the last event id", async () => {
    const { requests, states, controller, done } = harness([sse(frame("DelayChanged", { sequence: 1043 }, 1043)), sse("", { hold: true })]);
    await until(() => requests.length === 2 && states.filter((s) => s === "live").length === 2);

    expect(requests[1]!.headers["last-event-id"]).toBe("1043");   // the server then replays only what was missed
    expect(states).toEqual(["connecting", "live", "reconnecting", "live"]);
    controller.abort();
    await done;
  });

  it("retries after a server error", async () => {
    const { requests, states, controller, done } = harness([status(503), sse("", { hold: true })]);
    await until(() => states.includes("live"));

    expect(requests).toHaveLength(2);
    expect(states).toEqual(["connecting", "reconnecting", "live"]);
    controller.abort();
    await done;
  });

  it("retries after a dropped connection (the network call itself fails)", async () => {
    let first = true;
    const states: StreamConnection[] = [];
    const controller = new AbortController();
    const done = connectStream({
      lineIds: [LINE],
      getToken: async () => null,
      fetch: async (_input, init) => {
        if (first) {
          first = false;
          throw new TypeError("Failed to fetch");
        }
        return sse("", { hold: true })(init ?? {});
      },
      signal: controller.signal,
      onFrame: () => {},
      onConnection: (s) => states.push(s),
      retryDelayMs: () => 1,
    });
    await until(() => states.includes("live"));

    expect(states).toEqual(["connecting", "reconnecting", "live"]);
    controller.abort();
    await done;
  });

  it("does NOT retry a refused request (400): it fails, with a reason", async () => {
    const { requests, states, details, done } = harness([status(400), sse("", { hold: true })]);
    await done;

    expect(requests).toHaveLength(1);
    expect(states).toEqual(["connecting", "failed"]);
    expect(details.at(-1)).toContain("400");
  });

  it("answers a 401 by asking for a fresh token and trying once more", async () => {
    const { requests, states, controller, done } = harness([status(401), sse("", { hold: true })], ["old-token", "new-token"]);
    await until(() => states.includes("live"));

    expect(requests.map((r) => r.headers.authorization)).toEqual(["Bearer old-token", "Bearer new-token"]);
    controller.abort();
    await done;
  });

  it("gives up after a second 401 in a row instead of looping", async () => {
    const { requests, states, details, done } = harness([status(401)], ["t1", "t2", "t3"]);
    await done;

    expect(requests).toHaveLength(2);
    expect(states.at(-1)).toBe("failed");
    expect(details.at(-1)).toContain("sign in");
  });

  it("stops for good when the page leaves: no further request", async () => {
    const { requests, controller, done } = harness([sse("", { hold: true })]);
    await until(() => requests.length === 1);

    controller.abort();
    await done;
    await new Promise((resolve) => setTimeout(resolve, 30));

    expect(requests).toHaveLength(1);
  });
});
