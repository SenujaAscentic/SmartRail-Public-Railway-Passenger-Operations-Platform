import { expect, test } from "@playwright/test";
import { getJson, openJourney, record, signInAsController, stack, until } from "../support/helpers.ts";

// Flow 3: the controller cancels a train in the ops web (after seeing what it will affect) and the station display shows "Cancelled"
// within 5 s (SM-3). The change is audited with the actor. (Push is deferred: the passenger app learns of it through the same live
// stream, which the Flutter fixture tests cover.)

const colombo = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", hour: "2-digit", minute: "2-digit", hour12: false });

test("flow 3: a cancellation shows on the station display within 5 s and is audited", async ({ page, context }) => {
  const s = stack();
  await signInAsController(page);
  const journeyId = await openJourney(page, "1002"); // Kandy to Colombo Fort, 10:00, runs on weekdays

  // The board of Polgahawela, a stop of that train, before anything happens.
  const detailBefore = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  const plg = detailBefore.stops.find((x: any) => x.name === "Polgahawela");
  const timeOnBoard = colombo.format(new Date(plg.scheduledDeparture));
  const display = await context.newPage();
  await display.goto(`${s.display}/board/PLG`);
  const row = display.locator("li.line", { has: display.locator("time.line-time", { hasText: new RegExp(`^${timeOnBoard}$`) }) });
  await expect(row, "the train is on the board before the cancellation").toBeVisible({ timeout: 60_000 });
  await expect(row).not.toContainText("Cancelled");

  // The cancellation: the dialog shows what it affects before anything is sent.
  await page.getByRole("button", { name: "Cancel journey", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Cancel this journey?" });
  await expect(dialog.getByText("This will affect")).toBeVisible();
  await expect(dialog.getByText(/Polgahawela/)).toBeVisible();
  await dialog.getByLabel("Reason code").selectOption("TrainFault");
  await dialog.getByLabel(/Note/).fill("Engine failure (E2E flow 3)");
  await page.screenshot({ path: "../docs/qa/evidence/flow3-confirm-dialog.png" });
  const clickedAt = Date.now();
  await dialog.getByRole("button", { name: "Cancel journey" }).click();

  // SM-3: the display shows it within 5 s.
  const shown = await until("'Cancelled' on the board", async () => ((await row.innerText()).includes("Cancelled") ? true : undefined), 15, 100);
  const sm3 = shown.at - clickedAt;
  expect(sm3, `SM-3: the display must show it within 5 s (took ${sm3} ms)`).toBeLessThan(5_000);
  await display.screenshot({ path: "../docs/qa/evidence/flow3-board-cancelled.png" });

  // API state, then the audit trail with the actor.
  const detail = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  expect(detail.status.kind).toBe("Cancelled");
  expect(detail.reason.code).toBe("TrainFault");
  expect(detail.stops.every((x: any) => x.status === "Skipped")).toBe(true);

  await page.getByRole("link", { name: "Audit" }).click();
  await page.getByLabel("Journey id").fill(journeyId);
  await page.getByRole("button", { name: "Apply" }).click();
  const audit = page.getByRole("row", { name: /JourneyCancelled/ });
  await expect(audit).toBeVisible();
  await expect(audit).toContainText("CTRL-01");
  await page.screenshot({ path: "../docs/qa/evidence/flow3-audit.png" });

  record("flow3", { sm3_cancelToDisplayMs: sm3, sm3_limitMs: 5_000, journeyVersionAfter: detail.version, stopsSkipped: detail.stops.length, audited: true });
});
