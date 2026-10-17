import { describe, expect, it } from "vitest";
import { MAX_EVENTS, describeFrame, initialLiveState, liveReducer } from "./live-events.ts";

// The page reads ONLY the fields every event has (the envelope, domain model 8.1); the payload stays opaque JSON.
const delayChanged = { eventId: "2c9f5b81", sequence: 1043, type: "DelayChanged", version: 1, occurredAt: "2026-10-14T06:44:00Z", journeyId: "j-1", payload: { delayMinutes: 4 } };

describe("describeFrame", () => {
  it("reads sequence, time and journey from an event's envelope", () => {
    const event = describeFrame({ name: "DelayChanged", id: "1043", data: JSON.stringify(delayChanged) }, 1);

    expect(event).toMatchObject({ name: "DelayChanged", sequence: 1043, occurredAt: "2026-10-14T06:44:00Z", journeyId: "j-1" });
  });

  it("takes the sequence from the data when the frame has no id (a snapshot)", () => {
    const event = describeFrame({ name: "snapshot", id: "", data: JSON.stringify({ journeyId: "j-2", sequence: 1051, delayMinutes: 4 }) }, 2);

    expect(event).toMatchObject({ name: "snapshot", sequence: 1051, journeyId: "j-2", occurredAt: null });
  });

  it("works for a resync, which has only a reason", () => {
    const event = describeFrame({ name: "resync", id: "", data: JSON.stringify({ reason: "The missed events are no longer kept." }) }, 3);

    expect(event).toMatchObject({ name: "resync", sequence: null, journeyId: null });
  });

  it("does not trust the data: wrong types and bad JSON give nulls, and the raw text is kept", () => {
    expect(describeFrame({ name: "X", id: "", data: JSON.stringify({ sequence: "12", journeyId: 5, occurredAt: false }) }, 4)).toMatchObject({ sequence: null, journeyId: null, occurredAt: null });
    const broken = describeFrame({ name: "X", id: "", data: "{not json" }, 5);
    expect(broken).toMatchObject({ sequence: null, journeyId: null, raw: "{not json" });
  });

  it("uses the frame id when it is a whole number", () => {
    expect(describeFrame({ name: "X", id: "77", data: "{}" }, 6).sequence).toBe(77);
    expect(describeFrame({ name: "X", id: "abc", data: "{}" }, 7).sequence).toBeNull();
  });
});

describe("liveReducer", () => {
  const frame = (name: string, data: unknown = {}, id = "") => ({ type: "frame" as const, frame: { name, id, data: JSON.stringify(data) } });

  it("starts empty and 'connecting'", () => {
    expect(initialLiveState).toMatchObject({ connection: "connecting", events: [], resyncs: 0 });
  });

  it("follows the connection", () => {
    const state = liveReducer(initialLiveState, { type: "connection", state: "reconnecting", detail: undefined });

    expect(state.connection).toBe("reconnecting");
  });

  it("puts the newest event first", () => {
    let state = liveReducer(initialLiveState, frame("DelayChanged", { sequence: 1 }, "1"));
    state = liveReducer(state, frame("DelayChanged", { sequence: 2 }, "2"));

    expect(state.events.map((e) => e.sequence)).toEqual([2, 1]);
  });

  it("keeps only the most recent events", () => {
    let state = initialLiveState;
    for (let n = 1; n <= MAX_EVENTS + 5; n++) state = liveReducer(state, frame("DelayChanged", { sequence: n }, String(n)));

    expect(state.events).toHaveLength(MAX_EVENTS);
    expect(state.events[0]?.sequence).toBe(MAX_EVENTS + 5);
  });

  it("counts snapshots and events separately", () => {
    let state = liveReducer(initialLiveState, frame("snapshot", { journeyId: "a" }));
    state = liveReducer(state, frame("snapshot", { journeyId: "b" }));
    state = liveReducer(state, frame("DelayChanged", { sequence: 3 }, "3"));

    expect(state).toMatchObject({ snapshots: 2, received: 3 });
  });

  it("on a resync forgets the old events (they may be stale) and remembers that it happened", () => {
    let state = liveReducer(initialLiveState, frame("DelayChanged", { sequence: 1 }, "1"));
    state = liveReducer(state, frame("resync", { reason: "The missed events are no longer kept." }));

    expect(state.events.map((e) => e.name)).toEqual(["resync"]);
    expect(state.resyncs).toBe(1);
  });

  it("keeps the failure reason for the page to show", () => {
    const state = liveReducer(initialLiveState, { type: "connection", state: "failed", detail: "The server refused the request (400)." });

    expect(state).toMatchObject({ connection: "failed", detail: "The server refused the request (400)." });
  });
});
