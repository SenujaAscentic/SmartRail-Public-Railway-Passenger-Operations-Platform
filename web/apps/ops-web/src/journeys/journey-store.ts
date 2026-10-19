import type { components } from "@smartrail/api-client";
import type { StreamFrame } from "../stream/stream-client.ts";

export type Detail = components["schemas"]["JourneyDetailResponse"];

// Who supplied the actual times of one stop (staff only; the ops stream tells us).
export interface StopSources {
  arrival?: string;
  departure?: string;
}

// One journey as the ops screens hold it: the public detail (as the passengers see it) and where each value came from.
export interface Entry {
  detail: Detail;
  delaySource: string | null;
  delayAt: string | null;                     // when that delay was set, as far as we know (an event time, or the last actual time of the journey)
  stopSources: Record<string, StopSources>;   // by station id
  needsRefresh: boolean;                      // an event arrived that changes more than we read from it: ask the API again
}

export interface Store {
  journeys: Record<string, Entry>;
  resyncs: number;
}

export const emptyStore: Store = { journeys: {}, resyncs: 0 };

export type StoreAction =
  | { type: "frame"; frame: StreamFrame }
  | { type: "detail"; detail: Detail }     // the answer of GET /journeys/{id}, or the journey inside a 409
  | { type: "refreshing"; journeyId: string };

const isObject = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null && !Array.isArray(value);

function parse(data: string): Record<string, unknown> | null {
  try {
    const value: unknown = JSON.parse(data);
    return isObject(value) ? value : null;
  } catch {
    return null;
  }
}

// The ops screens never re-implement the rules: a snapshot gives a whole journey, and an event only says "this journey changed", so
// the journey is read again from the API (the backend's answer is the truth). Two things are applied at once because they are
// frequent and complete in themselves: the train's position (TrainPositionUpdated), and the SOURCE of a value, which only the
// ops stream carries.
export function storeReducer(store: Store, action: StoreAction): Store {
  if (action.type === "detail") {
    const old = store.journeys[action.detail.journeyId];
    return {
      ...store,
      journeys: {
        ...store.journeys,
        [action.detail.journeyId]: { delaySource: null, delayAt: null, stopSources: {}, ...old, detail: action.detail, needsRefresh: false },
      },
    };
  }

  if (action.type === "refreshing") {
    const entry = store.journeys[action.journeyId];
    return entry ? { ...store, journeys: { ...store.journeys, [action.journeyId]: { ...entry, needsRefresh: false } } } : store;
  }

  const { frame } = action;
  const data = parse(frame.data);
  if (data === null) return store;

  if (frame.name === "resync") return { ...store, journeys: {}, resyncs: store.resyncs + 1 };   // what is on screen may be stale: the snapshots refill it

  if (frame.name === "snapshot") {
    const journey = data.journey;
    if (!isObject(journey) || typeof data.journeyId !== "string") return store;
    const stopSources: Record<string, StopSources> = {};
    for (const stop of Array.isArray(data.stops) ? data.stops : []) {
      if (!isObject(stop) || typeof stop.stationId !== "string") continue;
      const sources: StopSources = {};
      if (typeof stop.actualArrivalSource === "string") sources.arrival = stop.actualArrivalSource;
      if (typeof stop.actualDepartureSource === "string") sources.departure = stop.actualDepartureSource;
      stopSources[stop.stationId] = sources;
    }
    return {
      ...store,
      journeys: {
        ...store.journeys,
        [data.journeyId]: {
          detail: journey as unknown as Detail,
          delaySource: typeof data.delaySource === "string" ? data.delaySource : null,
          delayAt: latestActual(journey as unknown as Detail),
          stopSources,
          needsRefresh: false,
        },
      },
    };
  }

  const journeyId = typeof data.journeyId === "string" ? data.journeyId : null;
  const entry = journeyId ? store.journeys[journeyId] : undefined;
  if (!journeyId || !entry) return store;   // a journey we do not hold (another line, or its snapshot has not come yet)

  const payload = isObject(data.payload) ? data.payload : {};
  const source = typeof data.source === "string" ? data.source : null;
  const occurredAt = typeof data.occurredAt === "string" ? data.occurredAt : null;

  if (frame.name === "TrainPositionUpdated") {
    const location = isObject(payload.location) ? payload.location : null;
    if (!location || typeof location.latitude !== "number" || typeof location.longitude !== "number") return store;
    const position = {
      latitude: location.latitude,
      longitude: location.longitude,
      speedKmh: Number(payload.speedKmh ?? 0),
      lastStationId: String(payload.lastStationId ?? ""),
      nextStationId: String(payload.nextStationId ?? ""),
      fractionToNext: Number(payload.fractionToNext ?? 0),
    };
    const names = entry.detail.stops.reduce<Record<string, string>>((all, stop) => ({ ...all, [stop.stationId]: stop.name }), {});
    return replace(store, journeyId, {
      ...entry,
      detail: {
        ...entry.detail,
        freshness: { ...entry.detail.freshness, tracking: "Live", updatedAt: occurredAt, lastStationName: names[position.lastStationId] ?? null, lastPosition: position },
      },
    });
  }

  let next: Entry = { ...entry, needsRefresh: true };
  if (frame.name === "DelayChanged" && source) next = { ...next, delaySource: source, delayAt: occurredAt };
  if (frame.name === "StopStatusChanged" && source && typeof payload.stationId === "string") {
    const sources = { ...entry.stopSources[payload.stationId] };
    if (payload.to === "Arrived") sources.arrival = source;
    if (payload.to === "Departed" || payload.to === "Passed") sources.departure = source;
    next = { ...next, stopSources: { ...entry.stopSources, [payload.stationId]: sources } };
  }
  return replace(store, journeyId, next);
}

// The latest time anything really happened on the journey (an actual arrival or departure): the age shown with a delay that came
// from the crew or GPS.
export function latestActual(detail: Detail): string | null {
  const times = detail.stops.flatMap((stop) => [stop.actualArrival, stop.actualDeparture]).filter((t): t is string => typeof t === "string");
  return times.length === 0 ? null : times.reduce((a, b) => (a > b ? a : b));
}

function replace(store: Store, journeyId: string, entry: Entry): Store {
  return { ...store, journeys: { ...store.journeys, [journeyId]: entry } };
}

// What a screen needs from the whole set.
export const journeyList = (store: Store): Entry[] => Object.values(store.journeys);

// A train the controller should look at (US-O2): late, cancelled, terminated early or held. On-time trains are left out.
export function isDisrupted(detail: Detail): boolean {
  const kind = detail.status.kind;
  return kind === "Late" || kind === "Cancelled" || kind === "TerminatedEarly" || detail.hold !== null;
}

// The order of the disrupted list: the most late first; a cancelled or terminated train counts as the worst.
export function lateness(detail: Detail): number {
  const kind = detail.status.kind;
  if (kind === "Cancelled" || kind === "TerminatedEarly") return Number.MAX_SAFE_INTEGER;
  return detail.status.delayMinutes ?? 0;
}
