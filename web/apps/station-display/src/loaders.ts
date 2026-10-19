import type { ApiClient } from "@smartrail/api-client";
import { BOARD_REQUEST_LIMIT } from "./rows.ts";
import type { BoardRow, JourneyDetail, LineInfo } from "./types.ts";

// The calls of the display pages. All of them are public GET requests: a display token cannot write anything.

export async function loadLines(api: ApiClient): Promise<LineInfo[]> {
  const { data } = await api.GET("/api/v1/lines");
  if (!data) throw new Error("The lines could not be loaded.");
  return data;
}

// The next departures of a station, more than the board shows (see rows.ts).
export async function loadBoard(api: ApiClient, stationId: string): Promise<BoardRow[]> {
  const { data } = await api.GET("/api/v1/stations/{stationId}/board", {
    params: { path: { stationId }, query: { kind: "departures", limit: String(BOARD_REQUEST_LIMIT) } },
  });
  if (!data) throw new Error("The board could not be loaded.");
  return data;
}

// What the platform display shows: the board, and the journey of the next train on the platform (for "Calling at").
export interface PlatformData {
  rows: BoardRow[];
  // The journey of the next train, only when it stops here; null otherwise or when it could not be loaded.
  journey: JourneyDetail | null;
}

export async function loadPlatform(api: ApiClient, stationId: string, platform: string): Promise<PlatformData> {
  const rows = await loadBoard(api, stationId);
  const next = rows.find((row) => row.platform === platform);
  if (!next || next.callType !== "Stop") return { rows, journey: null };

  // The calling points are a nice-to-have: if the journey cannot be read, the rest of the display still works.
  try {
    const { data } = await api.GET("/api/v1/journeys/{journeyId}", { params: { path: { journeyId: next.journeyId } } });
    return { rows, journey: data ?? null };
  } catch {
    return { rows, journey: null };
  }
}
