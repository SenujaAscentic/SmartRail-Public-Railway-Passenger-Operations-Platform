import { describe, expect, it } from "vitest";
import { readMainLineId } from "./config.ts";

// Until GET /lines exists (agent B will add it), the Main Line's id is a development setting.
describe("readMainLineId", () => {
  it("accepts a GUID, trimmed and lower-cased", () => {
    expect(readMainLineId({ VITE_MAIN_LINE_ID: "  5D1C8E0A-2B47-4F6A-9C33-71A0E4D8B2F6 " })).toEqual({ kind: "ok", lineId: "5d1c8e0a-2b47-4f6a-9c33-71a0e4d8b2f6" });
  });

  it("says 'missing' when it is not set or blank", () => {
    expect(readMainLineId({})).toEqual({ kind: "missing" });
    expect(readMainLineId({ VITE_MAIN_LINE_ID: "   " })).toEqual({ kind: "missing" });
  });

  it("says 'invalid' for anything that is not a GUID (so nothing odd is put into a URL)", () => {
    expect(readMainLineId({ VITE_MAIN_LINE_ID: "main-line" })).toEqual({ kind: "invalid" });
    expect(readMainLineId({ VITE_MAIN_LINE_ID: "5d1c8e0a-2b47-4f6a-9c33-71a0e4d8b2f6,evil" })).toEqual({ kind: "invalid" });
  });
});
