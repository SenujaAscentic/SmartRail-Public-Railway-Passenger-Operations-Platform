import { describe, expect, it } from "vitest";
import { formatColombo } from "./format.ts";

// Times are stored and sent in UTC; the app shows them in Sri Lanka time (UTC+5:30, no daylight saving).
describe("formatColombo", () => {
  it("shows a UTC time as Colombo time", () => {
    expect(formatColombo("2026-10-14T06:44:00Z")).toBe("14 Oct, 12:14:00");
  });

  it("crosses midnight correctly", () => {
    expect(formatColombo("2026-10-14T19:00:00Z")).toBe("15 Oct, 00:30:00");
  });

  it("shows a dash for a missing or unreadable time", () => {
    expect(formatColombo(null)).toBe("—");
    expect(formatColombo("not a time")).toBe("—");
  });
});
