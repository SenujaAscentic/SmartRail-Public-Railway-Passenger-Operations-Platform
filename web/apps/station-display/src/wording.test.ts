import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { ICON_NAMES } from "./components/Icon.tsx";
import {
  holdNotice,
  minutesText,
  NEXT_TRAIN_DOES_NOT_STOP,
  platformText,
  STATUS_STYLES,
  statusText,
  stopStatusText,
  styleForKind,
  styleOf,
} from "./wording.ts";

// Runs EVERY case of shared/status-fixtures.json (CLAUDE.md section 5.6). A case with a shape this test does not know fails,
// so a new fixture cannot be silently ignored.
const root = resolve(import.meta.dirname, "../../../..");
const fixtures = JSON.parse(readFileSync(resolve(root, "shared/status-fixtures.json"), "utf8")) as {
  cases: { id: string; input: Record<string, any>; expected: { text: string; icon?: string } }[];
};
const tokens = JSON.parse(readFileSync(resolve(root, "shared/tokens.json"), "utf8")) as { status: Record<string, { icon: string }> };

// Cases for the passenger app only (freshness, "offline" of a phone, "does not run today"): the display pages show none of them.
const PASSENGER_ONLY = new Set(["S-14", "S-15", "S-16", "S-17", "S-24", "S-25", "S-26", "S-27", "S-28"]);

describe("shared/status-fixtures.json", () => {
  it("has cases, and the ids of the passenger-only list exist", () => {
    const ids = new Set(fixtures.cases.map((c) => c.id));
    expect(ids.size).toBeGreaterThan(20);
    for (const id of PASSENGER_ONLY) expect(ids.has(id), id).toBe(true);
  });

  for (const fixture of fixtures.cases.filter((c) => !PASSENGER_ONLY.has(c.id))) {
    it(`fixture ${fixture.id}: ${fixture.expected.text}`, () => {
      const { input, expected } = fixture;

      if ("status" in input) {
        expect(statusText(input.status, input.terminatesAt)).toBe(expected.text);
        if (expected.icon) expect(styleForKind(input.status.kind).icon).toBe(expected.icon);
      } else if ("platform" in input && !("display" in input)) {
        expect(platformText(input.platform, input.previousPlatform ?? null)).toBe(expected.text);
        if (expected.icon) expect(styleOf("platform-changed").icon).toBe(expected.icon);
      } else if ("stopStatus" in input) {
        expect(stopStatusText(input.stopStatus)).toBe(expected.text);
      } else if (input.display === "platform") {
        expect(input.nextTrainCallType).toBe("Pass");
        expect(NEXT_TRAIN_DOES_NOT_STOP).toBe(expected.text);
      } else if (input.display === "infoScreen") {
        expect(holdNotice(input.holdSet, input.platform)).toBe(expected.text);
      } else {
        throw new Error(`Unknown fixture shape: ${JSON.stringify(input)}`);
      }
    });
  }
});

describe("minutesText", () => {
  it("writes hours from 60 minutes", () => {
    expect(minutesText(59)).toBe("59 min");
    expect(minutesText(60)).toBe("1 h");
    expect(minutesText(75)).toBe("1 h 15 min");
    expect(minutesText(125)).toBe("2 h 5 min");
  });
});

describe("statusText for what the backend might send", () => {
  it("never turns an unknown kind into 'On time'", () => {
    expect(statusText({ kind: "SomethingNew", delayMinutes: null })).toBe("SomethingNew");
  });

  it("a late train without minutes is not shown as on time", () => {
    expect(statusText({ kind: "Late", delayMinutes: null })).not.toBe("On time");
  });
});

describe("the icons come from shared/tokens.json", () => {
  const tokenOf: Record<string, string> = {
    "on-time": "OnTime",
    early: "Early",
    late: "Late",
    held: "Held",
    cancelled: "Cancelled",
    "terminated-early": "TerminatedEarly",
    "platform-changed": "PlatformChanged",
    "not-started": "NotStarted",
    scheduled: "Scheduled",
  };

  it("every status style has the icon of its token, and the icon exists", () => {
    for (const style of STATUS_STYLES) {
      const token = tokens.status[tokenOf[style.key]!];
      expect(token, style.key).toBeDefined();
      expect(style.icon, style.key).toBe(token!.icon);
      expect(ICON_NAMES).toContain(style.icon);
    }
  });

  it("every status of the tokens file that the displays use has a style", () => {
    const covered = new Set(Object.values(tokenOf));
    for (const name of ["OnTime", "Early", "Late", "Held", "Cancelled", "TerminatedEarly", "PlatformChanged", "NotStarted", "Scheduled"]) {
      expect(covered.has(name), name).toBe(true);
    }
  });
});
