import type { components } from "@smartrail/api-client";

type Detail = components["schemas"]["JourneyDetailResponse"];
type Line = components["schemas"]["LineResponse"];

// Ids used by the screen tests. Real-looking GUIDs, made up.
export const LINE_ID = "5d1c8e0a-2b47-4f6a-9c33-71a0e4d8b2f6";
export const FORT = "3f1b6a52-8c0d-4e7a-b1f4-2d9c5e7a0b13";
export const RAMBUKKANA = "9a4e2d71-5b38-4c06-8e1f-6a0d3b7c9e24";
export const KANDY = "c7d05f38-1e92-4ab6-9d70-4f8a2c1e6b35";

export const LINE: Line = {
  lineId: LINE_ID,
  code: "MAIN",
  name: "Main Line",
  routes: [
    {
      routeId: "11111111-0000-4000-8000-000000000001",
      code: "MAIN-UP",
      direction: "Up",
      name: "Colombo Fort to Kandy",
      stations: [
        { stationId: FORT, code: "FOT", name: "Colombo Fort", distanceKm: 0, latitude: 6.93, longitude: 79.85 },
        { stationId: RAMBUKKANA, code: "RBK", name: "Rambukkana", distanceKm: 80, latitude: 7.2, longitude: 80.4 },
        { stationId: KANDY, code: "KDT", name: "Kandy", distanceKm: 120, latitude: 7.29, longitude: 80.63 },
      ],
    },
  ],
};

export const T = (hhmm: string) => `2026-10-14T${hhmm}:00Z`;

// A journey Fort -> Rambukkana -> Kandy as GET /journeys/{id} and the stream snapshot describe it.
export function journey(overrides: Partial<Detail> & { id?: string } = {}): Detail {
  const { id = "0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01", ...rest } = overrides;
  return {
    journeyId: id,
    serviceNumber: "1001",
    name: "Kandy Express",
    trainType: "Express",
    serviceDate: "2026-10-14",
    status: { kind: "OnTime", delayMinutes: 0 },
    from: { stationId: FORT, name: "Colombo Fort", scheduledDeparture: T("01:00"), expectedDeparture: T("01:00"), platform: "1", previousPlatform: null },
    to: { stationId: KANDY, name: "Kandy", scheduledArrival: T("03:00"), expectedArrival: T("03:00") },
    freshness: { tracking: "Live", updatedAt: T("01:30"), lastStationName: "Colombo Fort", lastPosition: { latitude: 6.9, longitude: 79.9, speedKmh: 40, lastStationId: FORT, nextStationId: RAMBUKKANA, fractionToNext: 0.25 } },
    hold: null,
    reason: null,
    version: 3,
    stops: [
      stop(1, FORT, "Colombo Fort", "Departed", { scheduledDeparture: T("01:00"), expectedDeparture: T("01:00"), actualDeparture: T("01:00"), plannedPlatform: "1", platform: "1" }),
      stop(2, RAMBUKKANA, "Rambukkana", "Scheduled", { scheduledArrival: T("02:00"), expectedArrival: T("02:00"), scheduledDeparture: T("02:02"), expectedDeparture: T("02:02"), plannedPlatform: "2", platform: "2" }),
      stop(3, KANDY, "Kandy", "Scheduled", { scheduledArrival: T("03:00"), expectedArrival: T("03:00"), plannedPlatform: "3", platform: "3" }),
    ],
    trainSet: null,
    terminatesAt: null,
    ...rest,
  };
}

function stop(sequence: number, stationId: string, name: string, status: string, times: Partial<Detail["stops"][number]>): Detail["stops"][number] {
  return {
    sequence, stationId, name, callType: "Stop", status, isAdded: false,
    scheduledArrival: null, expectedArrival: null, actualArrival: null, scheduledDeparture: null, expectedDeparture: null, actualDeparture: null,
    plannedPlatform: "1", platform: "1", previousPlatform: null,
    ...times,
  };
}

// What the ops stream sends for one journey at connect time (the public snapshot plus the staff-only sources).
export function snapshot(detail: Detail, delaySource: string | null = "Crew", sequence = 10) {
  return {
    journeyId: detail.journeyId,
    serviceDate: detail.serviceDate,
    lineId: LINE_ID,
    status: "Running",
    delayMinutes: detail.status.delayMinutes ?? 0,
    tracking: detail.freshness.tracking,
    stops: detail.stops.map((s) => ({ stationId: s.stationId, sequence: s.sequence, status: s.status })),
    journey: detail,
    sequence,
    ...(delaySource ? { delaySource } : {}),
  };
}
