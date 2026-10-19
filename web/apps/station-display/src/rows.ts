import { toMillis } from "./format.ts";
import type { BoardRow, JourneyDetail, LineInfo } from "./types.ts";

// What the pages pick out of the board the backend sent. Selecting and grouping only: no time is worked out, no status is decided.

// Next 10 departures (US-D1 AC1). The backend also lists trains that only pass the station (callType "Pass"), for the
// platform display; a departures board shows the trains that stop here.
export const BOARD_SIZE = 10;
// So that passing trains cannot push stopping ones out of the list, the board is asked for more lines than it shows.
export const BOARD_REQUEST_LIMIT = 30;

// The lines of the departures board. Online the backend has already removed departed trains (after 1 minute) and kept cancelled
// ones until their time. Offline (no contact for 2 minutes) nothing live is known, so trains are removed by the clock as their
// scheduled time passes (US-D4 AC3).
export function boardRows(rows: BoardRow[], nowMillis: number, offline: boolean): BoardRow[] {
  return onePerJourney(rows)
    .filter((row) => row.callType === "Stop")
    .filter((row) => !offline || toMillis(row.time) >= nowMillis)
    .slice(0, BOARD_SIZE);
}

// A journey is shown once, whatever the list holds (the first line wins). This is a guard for the same journeyId twice; it
// does not hide two different journeys of the same service on different days (the backend must not send those).
function onePerJourney(rows: BoardRow[]): BoardRow[] {
  const seen = new Set<string>();
  return rows.filter((row) => !seen.has(row.journeyId) && seen.add(row.journeyId));
}

// One column of the departures board: the trains going one way along the line.
export interface Direction {
  key: string;
  // "Towards Kandy": the last station of the route in this direction.
  terminus: string;
  // "right" for the direction of the first route of the line, "left" for the way back (only the arrow in the heading).
  arrow: "right" | "left";
  rows: BoardRow[];
}

// Splits the lines by direction. A train belongs to the route (Fort to Kandy, or Kandy to Fort) on which its destination lies
// AFTER this station. A destination after an early termination is the termination station, which still lies on the right route.
// A route that ends at this station gives no column (nothing departs from a terminus in that direction).
export function groupByDirection(rows: BoardRow[], lines: LineInfo[], stationId: string): Direction[] {
  const columns: Direction[] = [];
  const routes = lines.flatMap((line) => line.routes);
  // "Up" (towards the far end, Kandy) first, as in the design.
  const ordered = [...routes].sort((a, b) => Number(b.direction === "Up") - Number(a.direction === "Up"));

  for (const route of ordered) {
    const index = route.stations.findIndex((s) => s.stationId === stationId);
    const last = route.stations.at(-1);
    if (index < 0 || last === undefined || index === route.stations.length - 1) continue;

    const after = new Set(route.stations.slice(index + 1).map((s) => s.stationId));
    columns.push({
      key: route.routeId,
      terminus: last.name,
      arrow: route.direction === "Up" ? "right" : "left",
      rows: rows.filter((row) => after.has(row.destination.stationId)),
    });
  }
  return columns;
}

// The platform display for one platform (US-D2).
export interface PlatformView {
  // The next train on this platform (it may only pass: callType "Pass", then "stand back from the platform edge").
  next: BoardRow | null;
  // The following two.
  then: BoardRow[];
  // Trains that were planned for this platform and now use another one: "Train 1520 to Kandy now departs from Platform 5".
  movedAway: BoardRow[];
}

export function platformView(rows: BoardRow[], platform: string, nowMillis: number, offline: boolean): PlatformView {
  // Offline, trains are removed by the clock, like on the board.
  const unique = onePerJourney(rows);
  const live = offline ? unique.filter((row) => toMillis(row.time) >= nowMillis) : unique;
  const here = live.filter((row) => row.platform === platform);
  return {
    next: here[0] ?? null,
    then: here.slice(1, 3),
    movedAway: live.filter((row) => row.previousPlatform === platform && row.platform !== platform),
  };
}

// "Calling at": the stations where the journey still stops after this station, in order (the stops it only passes are left out,
// a skipped stop too). The detail lists all stops with a sequence; this station's own sequence tells which are "after".
export function callingPointsAfter(journey: JourneyDetail, stationId: string): string[] {
  const here = journey.stops.find((stop) => stop.stationId === stationId);
  if (!here) return [];
  return journey.stops
    .filter((stop) => stop.sequence > here.sequence && stop.callType === "Stop" && stop.status !== "Skipped")
    .sort((a, b) => a.sequence - b.sequence)
    .map((stop) => stop.name);
}

// "Towards Kandy" on the platform display: the end of the route that the train runs along (null when the train's
// destination is not on a route that leaves this station).
export function platformDirection(row: BoardRow | null, lines: LineInfo[], stationId: string): string | null {
  if (row === null) return null;
  const column = groupByDirection([row], lines, stationId).find((c) => c.rows.length > 0);
  return column?.terminus ?? null;
}
