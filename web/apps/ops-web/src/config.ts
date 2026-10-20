// Settings that come from the environment of the build, not from the user.

const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export type MainLineSetting = { kind: "ok"; lineId: string } | { kind: "missing" } | { kind: "invalid" };

// An OPTIONAL override of the Main Line's id for the live page's stream filter (`lineIds`). Without it the page uses the first line of
// GET /lines. A value that is not a GUID is refused, so nothing odd ever ends up in a URL.
export function readMainLineId(env: Record<string, unknown> = import.meta.env): MainLineSetting {
  const value = typeof env.VITE_MAIN_LINE_ID === "string" ? env.VITE_MAIN_LINE_ID.trim() : "";
  if (value === "") return { kind: "missing" };
  return GUID.test(value) ? { kind: "ok", lineId: value.toLowerCase() } : { kind: "invalid" };
}
