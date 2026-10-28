import { expect, test } from "@playwright/test";
import { getJson, openJourney, record, signInAsController, stack } from "../support/helpers.ts";
import { useOwnStack } from "../support/own-stack.ts";
import { bearer, signInAs, staffToken } from "../support/staff.ts";

// N3: two controllers decide on the same train; the second one is told the train changed and nothing of theirs is applied.
//
// Given  train 2002 (not started); controller A and controller B (two sessions of the one seeded controller account CTRL-01) both have its
//        journey page open; B's screen is stale: its live stream is blocked, so it keeps the version it first read (as if B's network were slow)
// When   A sets a delay of 8 minutes (US-O4); the journey's control version goes up (R-18)
// And    B, still on the old version, sets a delay of 12 minutes
// Then   B sees "This train changed – review and try again." (409 VERSION_CONFLICT, R-18, contract 6), and
//        the journey still has A's 8 minutes and A's version, and the audit log has exactly one decision (A's), none of B's

const startupMs = useOwnStack("2026-10-21T09:30:00+05:30");

test("N3: the second of two controllers is told 'This train changed – review and try again.' and nothing of theirs is applied", async ({ page, browser }) => {
  const s = stack();

  // Controller A signs in and opens train 2002.
  await signInAsController(page);
  const journeyId = await openJourney(page, "2002");

  // Controller B signs in in a browser of their own, with the live stream blocked, and opens the same journey: a stale screen.
  const contextB = await browser.newContext();
  await contextB.route("**/api/v1/ops/stream**", (route) => route.abort());
  const pageB = await contextB.newPage();
  await signInAs(pageB, "CTRL-01");
  await pageB.goto(`${s.opsWeb}/journeys/${journeyId}`);
  await expect(pageB.getByRole("button", { name: "Delay", exact: true })).toBeVisible();
  const versionB = (await getJson(`${s.api}/api/v1/journeys/${journeyId}`)).version;

  // B opens the delay form and fills it, but does not send it yet.
  await pageB.getByRole("button", { name: "Delay", exact: true }).click();
  const formB = pageB.getByRole("form", { name: "Set a delay" });
  await formB.getByLabel("Delay in minutes").fill("12");
  await formB.getByLabel("Reason code").selectOption("Weather");

  // A sets 8 minutes first.
  await page.getByRole("button", { name: "Delay", exact: true }).click();
  const formA = page.getByRole("form", { name: "Set a delay" });
  await formA.getByLabel("Delay in minutes").fill("8");
  await formA.getByLabel("Reason code").selectOption("SignalFailure");
  await formA.getByRole("button", { name: "Set delay" }).click();
  await expect(page.getByText("Done. The change is on its way to everyone.")).toBeVisible();
  const afterA = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  expect(afterA.version, "A's decision raised the control version (R-18)").toBeGreaterThan(versionB);

  // B sends 12 minutes with the old version: B is told to look again.
  await formB.getByRole("button", { name: "Set delay" }).click();
  await expect(pageB.getByRole("alert").filter({ hasText: "This train changed – review and try again." })).toBeVisible();
  await pageB.screenshot({ path: "../docs/qa/evidence/n3-conflict.png" });

  // Nothing of B's was applied: A's delay and version stand, and the audit log has one decision.
  const after = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  expect(after.status).toMatchObject({ kind: "Late", delayMinutes: 8 });
  expect(after.version).toBe(afterA.version);
  const audit = (await (await fetch(`${s.api}/api/v1/ops/audit?journeyId=${journeyId}`, { headers: bearer(await staffToken("CTRL-01")) })).text());
  expect(audit.match(/DelaySet/g) ?? [], "one decision in the audit log: A's").toHaveLength(1);
  await contextB.close();

  record("n3", { train: "2002", versionStale: versionB, versionAfterA: afterA.version, delayShown: after.status.delayMinutes, auditDecisions: 1, stackStartupMs: startupMs(), clockSpeed: s.clockSpeed });
});
