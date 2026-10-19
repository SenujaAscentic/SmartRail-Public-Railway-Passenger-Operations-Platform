import type { components } from "@smartrail/api-client";

// The shapes the display pages read. They come from the GENERATED types (backend/openapi.yaml): a renamed or removed field
// stops `npm run typecheck`, not the demo. Nothing here describes the API by hand.
export type BoardRow = components["schemas"]["BoardRowResponse"];
export type LineInfo = components["schemas"]["LineResponse"];
export type JourneyDetail = components["schemas"]["JourneyDetailResponse"];
export type JourneyStatus = components["schemas"]["JourneyStatusResponse"];

// One station of the Main Line, found from the URL (its code, such as FOT, or its id).
export interface StationRef {
  id: string;
  code: string;
  name: string;
}
