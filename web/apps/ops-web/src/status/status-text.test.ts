import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { freshnessView, platformInfoText, platformText, statusView, stopText } from "./status-text.ts";

// CLAUDE.md section 5, step 6: the status wording comes only from shared/status-fixtures.json, and every app's tests run those cases.
interface Case {
  id: string;
  input: Record<string, unknown>;
  expected: { text: string; icon?: string };
}
const fixtures = JSON.parse(readFileSync(resolve(import.meta.dirname, "../../../../../shared/status-fixtures.json"), "utf-8")) as { now: string; cases: Case[] };
const NOW = Date.parse(fixtures.now);

function run(c: Case): { text: string; icon: string | undefined } | null {
  const i = c.input as {
    status?: { kind: string; delayMinutes?: number };
    terminatesAt?: string;
    holdSet?: boolean;
    platform?: string;
    previousPlatform?: string | null;
    stopStatus?: string;
    freshness?: { tracking: string; updatedAt?: string; lastSeenAt?: string; lastStation?: string };
    display?: string;
  };

  if (i.display === "infoScreen") return { text: platformInfoText(i.holdSet === true, i.platform ?? ""), icon: undefined };
  if (i.status) {
    const view = statusView({ kind: i.status.kind, delayMinutes: i.status.delayMinutes ?? null }, { terminatesAt: i.terminatesAt ?? null });
    return { text: view.text, icon: view.icon };
  }
  if (i.display === undefined && i.platform !== undefined) return { text: platformText(i.platform, i.previousPlatform), icon: undefined };
  if (i.stopStatus) return { text: stopText(i.stopStatus) ?? "", icon: undefined };
  if (i.freshness) {
    const view = freshnessView(
      { tracking: i.freshness.tracking, updatedAt: i.freshness.updatedAt ?? i.freshness.lastSeenAt ?? null, lastStationName: i.freshness.lastStation ?? null },
      NOW,
    );
    return { text: view.text, icon: view.icon };
  }
  return null;   // the offline case and the platform-display case belong to the passenger app and the displays
}

describe("status wording (shared/status-fixtures.json)", () => {
  for (const c of fixtures.cases) {
    const result = run(c);
    if (result === null) continue;
    it(`${c.id}: ${c.expected.text}`, () => {
      expect(result.text).toBe(c.expected.text);
      if (c.expected.icon && result.icon) expect(result.icon).toBe(c.expected.icon);
    });
  }

  it("S-08 also holds for the real API: a late train with a hold set says Held", () => {
    expect(statusView({ kind: "Late", delayMinutes: 3 }, { hold: true }).text).toBe("Held · 3 min late");
  });

  it("an on-time train that is held says so in words", () => {
    expect(statusView({ kind: "OnTime", delayMinutes: 0 }, { hold: true }).text).toBe("Held · on time");
  });
});
