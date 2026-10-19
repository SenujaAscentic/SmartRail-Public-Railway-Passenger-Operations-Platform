import { describe, expect, it } from "vitest";
import { formatClock, formatTime, toMillis } from "./format.ts";

describe("formatTime", () => {
  it("shows Colombo time (UTC+05:30), not UTC and not the computer's zone", () => {
    expect(formatTime("2026-10-14T02:08:00Z")).toBe("07:38");
  });

  it("pads hours and minutes and uses the 24-hour clock", () => {
    expect(formatTime("2026-10-14T00:00:00Z")).toBe("05:30");
    expect(formatTime("2026-10-14T12:30:00Z")).toBe("18:00");
  });

  it("goes past midnight into the next Colombo day", () => {
    expect(formatTime("2026-10-14T19:00:00Z")).toBe("00:30");
  });

  it("ignores seconds", () => {
    expect(formatTime("2026-10-14T02:08:59Z")).toBe("07:38");
  });

  it("shows dashes for a missing or unreadable time, never a wrong one", () => {
    expect(formatTime(null)).toBe("--:--");
    expect(formatTime(undefined)).toBe("--:--");
    expect(formatTime("not a time")).toBe("--:--");
  });
});

describe("formatClock and toMillis", () => {
  it("formats an instant given in milliseconds", () => {
    expect(formatClock(Date.parse("2026-10-14T02:08:00Z"))).toBe("07:38");
  });

  it("reads an ISO time as milliseconds", () => {
    expect(toMillis("2026-10-14T00:00:00Z")).toBe(Date.parse("2026-10-14T00:00:00Z"));
  });
});
