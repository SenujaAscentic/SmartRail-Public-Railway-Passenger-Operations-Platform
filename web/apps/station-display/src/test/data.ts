import type { BoardRow, JourneyDetail, LineInfo, StationRef } from "../types.ts";

// Made-up but contract-shaped data for the tests: a short Main Line (Fort, Maradana, Gampaha, Polgahawela, Kandy).
export const FORT = "10000000-0000-4000-8000-000000000001";
export const MARADANA = "10000000-0000-4000-8000-000000000002";
export const GAMPAHA = "10000000-0000-4000-8000-000000000003";
export const POLGAHAWELA = "10000000-0000-4000-8000-000000000004";
export const KANDY = "10000000-0000-4000-8000-000000000005";
export const VEYANGODA = "10000000-0000-4000-8000-0000000000aa";

const names: Record<string, [string, string]> = {
  [FORT]: ["FOT", "Colombo Fort"],
  [MARADANA]: ["MDA", "Maradana"],
  [GAMPAHA]: ["GPH", "Gampaha"],
  [POLGAHAWELA]: ["PLG", "Polgahawela"],
  [KANDY]: ["KDT", "Kandy"],
  [VEYANGODA]: ["VYG", "Veyangoda"],
};

export const station = (id: string): StationRef => ({ id, code: names[id]![0], name: names[id]![1] });
const routeStation = (id: string, km: number) => ({ stationId: id, code: names[id]![0], name: names[id]![1], distanceKm: km, latitude: 7, longitude: 80 });
const ref = (id: string) => ({ stationId: id, name: names[id]![1] });

export const LINES: LineInfo[] = [
  {
    lineId: "20000000-0000-4000-8000-000000000001",
    code: "MAIN",
    name: "Main Line",
    routes: [
      {
        routeId: "30000000-0000-4000-8000-000000000001",
        code: "MAIN-UP",
        direction: "Up",
        name: "Fort to Kandy",
        stations: [routeStation(FORT, 0), routeStation(MARADANA, 5), routeStation(GAMPAHA, 30), routeStation(POLGAHAWELA, 60), routeStation(KANDY, 120)],
      },
      {
        routeId: "30000000-0000-4000-8000-000000000002",
        code: "MAIN-DOWN",
        direction: "Down",
        name: "Kandy to Fort",
        stations: [routeStation(KANDY, 0), routeStation(POLGAHAWELA, 60), routeStation(GAMPAHA, 90), routeStation(MARADANA, 115), routeStation(FORT, 120)],
      },
    ],
  },
];

// A board line, by default a train to Kandy at 07:12 (01:42 UTC) on Platform 1, on time. Override what a test needs.
export function row(overrides: Partial<BoardRow> = {}): BoardRow {
  return {
    journeyId: "40000000-0000-4000-8000-000000000001",
    serviceNumber: "1001",
    trainType: "Express",
    origin: ref(FORT),
    destination: ref(KANDY),
    time: "2026-10-20T01:42:00Z",
    expectedTime: "2026-10-20T01:42:00Z",
    platform: "1",
    previousPlatform: null,
    callType: "Stop",
    stopStatus: "Scheduled",
    status: { kind: "OnTime", delayMinutes: 0 },
    hold: false,
    reason: null,
    ...overrides,
  };
}

// The nth train towards Kandy (service 1001, 1002, ...) and the nth towards Colombo Fort (2001, 2002, ...).
export const toKandy = (n: number, overrides: Partial<BoardRow> = {}) =>
  row({ journeyId: `40000000-0000-4000-8000-0000000001${String(n).padStart(2, "0")}`, serviceNumber: `10${String(n).padStart(2, "0")}`, ...overrides });

export const toFort = (n: number, overrides: Partial<BoardRow> = {}) =>
  row({
    journeyId: `40000000-0000-4000-8000-0000000002${String(n).padStart(2, "0")}`,
    serviceNumber: `20${String(n).padStart(2, "0")}`,
    origin: ref(KANDY),
    destination: ref(FORT),
    platform: "2",
    ...overrides,
  });

// A journey detail with the stops of the Up route (Veyangoda is only passed), for "Calling at".
export function journey(overrides: Partial<JourneyDetail> = {}): JourneyDetail {
  const stop = (sequence: number, id: string, callType: "Stop" | "Pass", status = "Scheduled") => ({
    sequence,
    stationId: id,
    name: names[id]![1],
    callType,
    status,
    isAdded: false,
    scheduledArrival: null,
    expectedArrival: null,
    actualArrival: null,
    scheduledDeparture: null,
    expectedDeparture: null,
    actualDeparture: null,
    plannedPlatform: "1",
    platform: "1",
    previousPlatform: null,
  });
  return {
    journeyId: "40000000-0000-4000-8000-000000000001",
    serviceNumber: "1001",
    name: "Kandy Express",
    trainType: "Express",
    serviceDate: "2026-10-20",
    status: { kind: "OnTime", delayMinutes: 0 },
    from: { stationId: FORT, name: "Colombo Fort", scheduledDeparture: null, expectedDeparture: null, platform: "5", previousPlatform: null },
    to: { stationId: KANDY, name: "Kandy", scheduledArrival: null, expectedArrival: null },
    freshness: { tracking: "Live", updatedAt: null, lastStationName: null, lastPosition: null },
    hold: null,
    reason: null,
    version: 1,
    stops: [
      stop(1, FORT, "Stop", "Departed"),
      stop(2, MARADANA, "Stop", "Departed"),
      stop(3, GAMPAHA, "Stop"),
      stop(4, VEYANGODA, "Pass"),
      stop(5, POLGAHAWELA, "Stop"),
      stop(6, KANDY, "Stop"),
    ],
    trainSet: null,
    terminatesAt: null,
    ...overrides,
  } as JourneyDetail;
}
