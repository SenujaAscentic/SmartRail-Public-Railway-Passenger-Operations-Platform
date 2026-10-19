import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { fakeStream } from "../test/sse.ts";
import { LiveFeed, OFFLINE_AFTER_MS, POLL_MS, REFETCH_DELAY_MS } from "./live-feed.ts";

// The feed with fake time: nothing waits for real seconds. `load` counts how often the data was read.
const STATION = "10000000-0000-4000-8000-000000000003";

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

function setup(options: { load?: () => Promise<number>; silenceMs?: number } = {}) {
  const stream = fakeStream();
  let calls = 0;
  const load = options.load ?? (async () => ++calls);
  const counted = vi.fn(load);
  const feed = new LiveFeed<number>({
    stationId: STATION,
    load: counted,
    streamFetch: stream.fetch,
    streamRetryDelayMs: () => 1000,
    ...(options.silenceMs !== undefined ? { streamSilenceMs: options.silenceMs } : {}),
  });
  return { feed, stream, load: counted };
}

const run = (ms: number) => vi.advanceTimersByTimeAsync(ms);

describe("starting", () => {
  it("loads the data and reads the stream of this station", async () => {
    const { feed, stream, load } = setup();
    feed.start();
    await run(0);

    expect(load).toHaveBeenCalledTimes(2); // once at the start and once when the stream opens (full state)
    expect(feed.getSnapshot().data).toBe(2);
    expect(stream.last().url).toBe(`/api/v1/stream?stationIds=${STATION}`);
    expect(feed.getSnapshot().connection).toBe("live");
    feed.stop();
  });

  it("sends no token (the stream is public)", async () => {
    const { feed, stream } = setup();
    feed.start();
    await run(0);

    expect(Object.keys(stream.last().headers).map((h) => h.toLowerCase())).not.toContain("authorization");
    feed.stop();
  });

  it("has no data and is connecting before the first answer", () => {
    const { feed } = setup({ load: () => new Promise(() => {}) });
    feed.start();

    const state = feed.getSnapshot();
    expect(state.data).toBeNull();
    expect(state.connection).toBe("connecting");
    expect(state.offline).toBe(false);
    feed.stop();
  });

  it("tells its listeners when something changes", async () => {
    const { feed } = setup();
    const listener = vi.fn();
    feed.subscribe(listener);
    feed.start();
    await run(0);

    expect(listener).toHaveBeenCalled();
    feed.stop();
  });
});

describe("events", () => {
  it("any event loads the data again, once after a short pause", async () => {
    const { feed, stream, load } = setup();
    feed.start();
    await run(0);
    const before = load.mock.calls.length;

    stream.event("DelayChanged", { sequence: 5 }, 5);
    await run(REFETCH_DELAY_MS - 50);
    expect(load.mock.calls.length).toBe(before);
    await run(100);
    expect(load.mock.calls.length).toBe(before + 1);
    feed.stop();
  });

  it("a burst of events causes one load", async () => {
    const { feed, stream, load } = setup();
    feed.start();
    await run(0);
    const before = load.mock.calls.length;

    stream.event("StopStatusChanged", {}, 1);
    await run(100);
    stream.event("DelayChanged", {}, 2);
    await run(100);
    stream.event("PlatformChanged", {}, 3);
    await run(1000);

    expect(load.mock.calls.length).toBe(before + 1);
    feed.stop();
  });

  it("an event of a kind the display has never seen loads the data too", async () => {
    const { feed, stream, load } = setup();
    feed.start();
    await run(0);
    const before = load.mock.calls.length;

    stream.event("SomethingNew", {}, 9);
    await run(1000);

    expect(load.mock.calls.length).toBe(before + 1);
    feed.stop();
  });

  it("a heartbeat is no event: it loads nothing", async () => {
    const { feed, stream, load } = setup();
    feed.start();
    await run(0);
    const before = load.mock.calls.length;

    stream.heartbeat();
    await run(5000);

    expect(load.mock.calls.length).toBe(before);
    feed.stop();
  });

  it("the newer answer wins when an older, slower one arrives later", async () => {
    const resolvers: ((value: number) => void)[] = [];
    const { feed, stream } = setup({ load: () => new Promise<number>((resolve) => resolvers.push(resolve)) });
    feed.start();
    await run(0);
    // two loads are open: the start and the stream's open
    expect(resolvers).toHaveLength(2);

    resolvers[1]!(200); // the newer one answers first
    await run(0);
    resolvers[0]!(100); // the older one answers late
    await run(0);

    expect(feed.getSnapshot().data).toBe(200);
    stream.heartbeat();
    feed.stop();
  });
});

describe("a broken stream", () => {
  it("when the server ends the stream: reconnecting, a poll every 30 s, and a full reload when it is back", async () => {
    const { feed, stream, load } = setup();
    feed.start();
    await run(0);

    stream.close();
    await run(10);
    expect(feed.getSnapshot().connection).toBe("reconnecting");

    // The retry waits 1 s; meanwhile the poll has not fired yet.
    await run(1000);
    expect(stream.connections).toHaveLength(2);
    expect(feed.getSnapshot().connection).toBe("live");
    expect(load.mock.calls.length).toBe(3); // 2 at the start, 1 more when the new stream opened
    feed.stop();
  });

  it("while the stream is down, the data is loaded every 30 seconds", async () => {
    const { feed, stream, load } = setup();
    feed.start();
    await run(0);
    stream.state.refuse = true;
    stream.close();
    await run(10);
    const before = load.mock.calls.length;

    await run(POLL_MS - 100);
    expect(load.mock.calls.length).toBe(before);
    await run(200);
    expect(load.mock.calls.length).toBe(before + 1);
    await run(POLL_MS);
    expect(load.mock.calls.length).toBe(before + 2);
    feed.stop();
  });

  it("a stream that keeps failing does not push the poll back", async () => {
    const { feed, stream, load } = setup();
    feed.start();
    await run(0);
    stream.state.refuse = true;
    stream.close();
    await run(10);
    const before = load.mock.calls.length;

    // The reconnect attempts fail every second; the poll must still come at 30 s.
    await run(POLL_MS + 200);

    expect(load.mock.calls.length).toBe(before + 1);
    feed.stop();
  });

  it("when the stream is back the poll stops", async () => {
    const { feed, stream, load } = setup();
    feed.start();
    await run(0);
    stream.state.refuse = true;
    stream.close();
    await run(5000);
    stream.state.refuse = false;
    await run(2000); // the next retry connects
    expect(feed.getSnapshot().connection).toBe("live");
    const before = load.mock.calls.length;

    // 90 s with heartbeats (without them the silence rule would reconnect, which loads again on purpose).
    for (let i = 0; i < 6; i++) {
      await run(15_000);
      stream.heartbeat();
    }

    expect(load.mock.calls.length).toBe(before);
    feed.stop();
  });

  it("resumes with Last-Event-ID", async () => {
    const { feed, stream } = setup();
    feed.start();
    await run(0);
    stream.event("DelayChanged", {}, 42);
    await run(10);

    stream.close();
    await run(1100);

    expect(stream.connections).toHaveLength(2);
    expect(stream.last().headers["Last-Event-ID"]).toBe("42");
    feed.stop();
  });

  it("no bytes for the silence time: the connection is closed and opened again", async () => {
    const { feed, stream } = setup({ silenceMs: 45_000 });
    feed.start();
    await run(0);
    const first = stream.last();

    await run(44_000);
    expect(stream.connections).toHaveLength(1);
    await run(2_500); // 45 s of silence, then the 1 s pause
    expect(first.open).toBe(false);
    expect(stream.connections.length).toBeGreaterThanOrEqual(2);
    feed.stop();
  });

  it("heartbeats keep the connection open", async () => {
    const { feed, stream } = setup({ silenceMs: 45_000 });
    feed.start();
    await run(0);

    for (let i = 0; i < 8; i++) {
      await run(15_000);
      stream.heartbeat();
    }

    expect(stream.connections).toHaveLength(1);
    feed.stop();
  });
});

describe("loads that fail", () => {
  it("the old data stays and the failure is noted; the next good load clears it", async () => {
    let fail = false;
    let n = 0;
    const { feed, stream } = setup({
      load: async () => {
        if (fail) throw new Error("down");
        return ++n;
      },
    });
    feed.start();
    await run(0);
    const good = feed.getSnapshot().data;

    fail = true;
    stream.event("DelayChanged", {}, 1);
    await run(1000);
    expect(feed.getSnapshot().data).toBe(good);
    expect(feed.getSnapshot().loadFailed).toBe(true);

    fail = false;
    stream.event("DelayChanged", {}, 2);
    await run(1000);
    expect(feed.getSnapshot().loadFailed).toBe(false);
    expect(feed.getSnapshot().data).toBeGreaterThan(good!);
    feed.stop();
  });
});

describe("offline after 2 minutes without contact (US-D4)", () => {
  it("is not offline while heartbeats arrive", async () => {
    const { feed, stream } = setup();
    feed.start();
    await run(0);

    for (let i = 0; i < 12; i++) {
      await run(15_000);
      stream.heartbeat();
    }

    expect(feed.getSnapshot().offline).toBe(false);
    feed.stop();
  });

  it("is offline after 2 minutes of silence from everything, and not before", async () => {
    const { feed, stream } = setup({ silenceMs: 10 * 60_000 });
    feed.start();
    await run(0);
    stream.state.refuse = true;
    const failing = vi.fn(async () => {
      throw new Error("down");
    });
    // From now on nothing works: the loads fail too.
    (feed as unknown as { options: { load: () => Promise<number> } }).options.load = failing;

    await run(OFFLINE_AFTER_MS - 2000);
    expect(feed.getSnapshot().offline).toBe(false);
    await run(3000);
    expect(feed.getSnapshot().offline).toBe(true);
    feed.stop();
  });

  it("a poll that works keeps the display online, although the stream is down", async () => {
    const { feed, stream } = setup();
    feed.start();
    await run(0);
    stream.state.refuse = true;
    stream.close();

    await run(OFFLINE_AFTER_MS * 3);

    expect(feed.getSnapshot().offline).toBe(false);
    feed.stop();
  });

  it("is online again as soon as bytes arrive", async () => {
    const { feed, stream } = setup({ silenceMs: 10 * 60_000 });
    feed.start();
    await run(0);
    (feed as unknown as { options: { load: () => Promise<number> } }).options.load = async () => {
      throw new Error("down");
    };
    await run(OFFLINE_AFTER_MS + 2000);
    expect(feed.getSnapshot().offline).toBe(true);

    stream.heartbeat();
    await run(10);

    expect(feed.getSnapshot().offline).toBe(false);
    feed.stop();
  });

  it("without any contact since the start, the first 2 minutes are not offline either", async () => {
    const { feed } = setup({ load: () => new Promise(() => {}) });
    feed.start();
    await run(OFFLINE_AFTER_MS - 5000);
    // (the stream connected, so there is contact; this checks that a slow first load alone does not make it offline)
    expect(feed.getSnapshot().offline).toBe(false);
    feed.stop();
  });
});

describe("stopping", () => {
  it("closes the stream and stops every timer", async () => {
    const { feed, stream, load } = setup();
    feed.start();
    await run(0);
    const connection = stream.last();
    stream.event("DelayChanged", {}, 1);
    await run(50); // a load is waiting

    feed.stop();
    const before = load.mock.calls.length;
    await run(POLL_MS * 5);

    expect(connection.open).toBe(false);
    expect(load.mock.calls.length).toBe(before);
    expect(stream.connections).toHaveLength(1);
  });

  it("can be started again (React runs effects twice in development)", async () => {
    const { feed, stream } = setup();
    feed.start();
    await run(0);
    feed.stop();
    await run(10);

    feed.start();
    await run(0);

    expect(stream.connections).toHaveLength(2);
    expect(feed.getSnapshot().connection).toBe("live");
    feed.stop();
  });
});
