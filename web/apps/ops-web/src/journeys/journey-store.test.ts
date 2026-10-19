import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { journey, snapshot, T, FORT, RAMBUKKANA } from "../test/fixtures.ts";
import { emptyStore, isDisrupted, lateness, storeReducer, type Store } from "./journey-store.ts";

const frame = (name: string, data: unknown) => ({ name, id: "1", data: JSON.stringify(data) });
const loaded = (): Store => storeReducer(emptyStore, { type: "frame", frame: frame("snapshot", snapshot(journey(), "Crew")) });
const id = journey().journeyId;

describe("the journey store", () => {
  it("fills from a snapshot, with who supplied the delay and each actual time", () => {
    const withSources = { ...snapshot(journey(), "Gps"), stops: [{ stationId: FORT, sequence: 1, status: "Departed", actualDepartureSource: "Crew" }] };
    const store = storeReducer(emptyStore, { type: "frame", frame: frame("snapshot", withSources) });

    expect(store.journeys[id]?.detail.serviceNumber).toBe("1001");
    expect(store.journeys[id]?.delaySource).toBe("Gps");
    expect(store.journeys[id]?.stopSources[FORT]).toEqual({ departure: "Crew" });
    expect(store.journeys[id]?.needsRefresh).toBe(false);
  });

  it("takes the age of the delay from delaySetAt in the staff snapshot", () => {
    const store = storeReducer(emptyStore, { type: "frame", frame: frame("snapshot", { ...snapshot(journey(), "Controller"), delaySetAt: T("01:20") }) });

    expect(store.journeys[id]?.delayAt).toBe(T("01:20"));
    expect(store.journeys[id]?.delaySource).toBe("Controller");
  });

  it("marks a journey for a fresh read when an event changes it, and remembers the source of the new delay", () => {
    const store = storeReducer(loaded(), {
      type: "frame",
      frame: frame("DelayChanged", { journeyId: id, source: "Controller", occurredAt: T("01:40"), payload: { delayMinutes: 9 } }),
    });

    expect(store.journeys[id]?.needsRefresh).toBe(true);
    expect(store.journeys[id]?.delaySource).toBe("Controller");
    expect(store.journeys[id]?.delayAt).toBe(T("01:40"));
  });

  it("remembers the source of a stop time", () => {
    const store = storeReducer(loaded(), {
      type: "frame",
      frame: frame("StopStatusChanged", { journeyId: id, source: "Gps", payload: { stationId: RAMBUKKANA, to: "Arrived" } }),
    });

    expect(store.journeys[id]?.stopSources[RAMBUKKANA]).toEqual({ arrival: "Gps" });
  });

  it("applies a new position at once, without asking the API", () => {
    const store = storeReducer(loaded(), {
      type: "frame",
      frame: frame("TrainPositionUpdated", {
        journeyId: id, occurredAt: T("01:45"),
        payload: { location: { latitude: 7.0, longitude: 80.0 }, speedKmh: 55, lastStationId: FORT, nextStationId: RAMBUKKANA, fractionToNext: 0.5 },
      }),
    });

    const freshness = store.journeys[id]?.detail.freshness;
    expect(freshness?.lastPosition?.fractionToNext).toBe(0.5);
    expect(freshness?.updatedAt).toBe(T("01:45"));
    expect(freshness?.lastStationName).toBe("Colombo Fort");
    expect(store.journeys[id]?.needsRefresh).toBe(false);
  });

  it("ignores an event for a journey it does not hold, and text that is not JSON", () => {
    const store = loaded();

    expect(storeReducer(store, { type: "frame", frame: frame("DelayChanged", { journeyId: "unknown", source: "Gps" }) })).toBe(store);
    expect(storeReducer(store, { type: "frame", frame: { name: "DelayChanged", id: "2", data: "not json" } })).toBe(store);
  });

  it("forgets everything on a resync (the snapshots refill it)", () => {
    const store = storeReducer(loaded(), { type: "frame", frame: frame("resync", { reason: "x" }) });

    expect(Object.keys(store.journeys)).toHaveLength(0);
    expect(store.resyncs).toBe(1);
  });

  it("takes the answer of the API as the journey, and keeps its sources", () => {
    const store = storeReducer(loaded(), { type: "detail", detail: journey({ version: 4 }) });

    expect(store.journeys[id]?.detail.version).toBe(4);
    expect(store.journeys[id]?.delaySource).toBe("Crew");
  });

  it("calls a train disrupted when it is late, cancelled, terminated or held, and sorts the worst first", () => {
    expect(isDisrupted(journey())).toBe(false);
    expect(isDisrupted(journey({ status: { kind: "Late", delayMinutes: 4 } }))).toBe(true);
    expect(isDisrupted(journey({ status: { kind: "Cancelled", delayMinutes: null } }))).toBe(true);
    expect(isDisrupted(journey({ hold: { stationId: FORT, minutes: 5, reason: null } }))).toBe(true);
    expect(lateness(journey({ status: { kind: "Cancelled", delayMinutes: null } }))).toBeGreaterThan(lateness(journey({ status: { kind: "Late", delayMinutes: 90 } })));
  });

  // The example files are what the backend promises (CLAUDE.md section 5, step 5): the store must read the real snapshot shape.
  it("reads the shared snapshot example, with its full journey detail", () => {
    const example = readFileSync(resolve(import.meta.dirname, "../../../../../shared/events/snapshot.json"), "utf-8");

    const store = storeReducer(emptyStore, { type: "frame", frame: { name: "snapshot", id: "1051", data: example } });

    const entry = Object.values(store.journeys)[0];
    expect(entry?.detail.serviceNumber).toBe("1001");
    expect(entry?.detail.stops).toHaveLength(3);
    expect(entry?.detail.freshness.lastPosition?.fractionToNext).toBe(0.42);
    expect(entry?.detail.terminatesAt).toBeNull();
  });
});
