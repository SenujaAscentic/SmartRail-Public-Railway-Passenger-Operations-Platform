import { expect, test } from "@playwright/test";
import { getJson, openJourney, record, signInAsController, stack } from "../support/helpers.ts";
import { useOwnStack } from "../support/own-stack.ts";

// W3: the controller cancels a train that has not started, then reinstates it, before its scheduled start. The board follows both steps, and
// the audit log keeps both decisions (who, when, why).
//
// Given  the demo clock is about 09:52 on Wed 21 Oct; train 2002 (Kandy to Colombo Fort, 10:30, not started) is on the board of Polgahawela
//        as "Not started" (S-09)
// When   the controller cancels it with the reason "Train fault" (US-O4, R-14)
// Then   the board row says "Cancelled" (S-06)
// When   the controller reinstates it (R-14: only before the scheduled start)
// Then   the board row is back to "Not started" (S-09) and no longer says "Cancelled"
// And    the audit log of the journey has two rows, newest first: JourneyReinstated and JourneyCancelled, each with the staff number
//        CTRL-01 and a time; the cancel row has the reason code TrainFault (the contract gives a reinstatement no reason: its Reason shows "—")
// Latencies (click to text visible) go to docs/qa/evidence/w3.json. SM-3 limit: 5 s.

// 2002 leaves Kandy at 10:30; the stack takes about 30 real seconds (2.5 demo minutes) to come up, so a start at 09:50 leaves
// about 35 demo minutes (7 real minutes) to cancel and reinstate before the scheduled start.
const startupMs = useOwnStack("2026-10-21T09:50:00+05:30");
const colombo = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", hour: "2-digit", minute: "2-digit", hour12: false });

test("W3: cancel a not-started train, then reinstate it: the board follows, the audit log has both rows", async ({ page, context }) => {
  const s = stack();

  // The controller signs in and opens train 2002.
  await signInAsController(page);
  const journeyId = await openJourney(page, "2002");

  // The board of Polgahawela shows the train as "Not started".
  const detail = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  const plg = detail.stops.find((x: any) => x.name === "Polgahawela");
  const timeOnBoard = colombo.format(new Date(plg.scheduledDeparture));
  const board = await context.newPage();
  await board.goto(`${s.display}/board/PLG`);
  const row = board.getByRole("listitem").filter({ has: board.getByText(timeOnBoard, { exact: true }) });
  await expect(row, "the train is on the board before the cancellation").toBeVisible({ timeout: 60_000 });
  await expect(row).toContainText("Not started");
  await expect(row).not.toContainText("Cancelled");

  // The controller cancels the journey: the dialog shows what it affects, then the reason code and a note, then confirms.
  await page.bringToFront();
  await page.getByRole("button", { name: "Cancel journey", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Cancel this journey?" });
  await expect(dialog.getByText("This will affect")).toBeVisible();
  await dialog.getByLabel("Reason code").selectOption("TrainFault");
  await dialog.getByLabel(/Note/).fill("Engine failure (E2E W3)");
  const cancelledAt = Date.now();
  await dialog.getByRole("button", { name: "Cancel journey" }).click();

  // The board row says "Cancelled".
  await expect(row).toContainText("Cancelled", { timeout: 15_000 });
  const cancelMs = Date.now() - cancelledAt;
  expect(cancelMs, "SM-3: the board within 5 s of the cancellation").toBeLessThan(5_000);
  await board.screenshot({ path: "../docs/qa/evidence/w3-board-cancelled.png" });

  // The controller reinstates the journey (the button is enabled once the page knows it is cancelled).
  const reinstate = page.getByRole("button", { name: "Reinstate", exact: true });
  await expect(reinstate).toBeEnabled();
  const reinstatedAt = Date.now();
  await reinstate.click();

  // The board row is back to "Not started" and no longer says "Cancelled".
  await expect(row).toContainText("Not started", { timeout: 15_000 });
  await expect(row).not.toContainText("Cancelled");
  const reinstateMs = Date.now() - reinstatedAt;
  expect(reinstateMs, "SM-3: the board within 5 s of the reinstatement").toBeLessThan(5_000);
  await board.screenshot({ path: "../docs/qa/evidence/w3-board-reinstated.png" });
  const after = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  expect(after.status.kind).toBe("NotStarted");
  expect(after.stops.some((x: any) => x.status === "Skipped"), "no stop stays skipped").toBe(false);

  // The audit log of this journey has two rows: who, when and why.
  await page.getByRole("link", { name: "Audit" }).click();
  await page.getByLabel("Journey id").fill(journeyId);
  await page.getByRole("button", { name: "Apply" }).click();
  const cancelRow = page.getByRole("row", { name: /JourneyCancelled/ });
  const reinstateRow = page.getByRole("row", { name: /JourneyReinstated/ });
  await expect(cancelRow).toBeVisible();
  await expect(reinstateRow).toBeVisible();
  await expect(page.getByRole("row")).toHaveCount(3); // the header and the two decisions
  for (const auditRow of [cancelRow, reinstateRow]) {
    await expect(auditRow).toContainText("CTRL-01");                                // who
    await expect(auditRow).toContainText(/\d{1,2} \w+, \d{2}:\d{2}:\d{2}/);        // when (Sri Lanka time)
  }
  await expect(cancelRow).toContainText("TrainFault");                              // why
  await expect(page.getByRole("row").nth(1), "newest first").toContainText("JourneyReinstated");
  await page.screenshot({ path: "../docs/qa/evidence/w3-audit.png" });

  record("w3", { train: "2002", w3_cancelToBoardMs: cancelMs, w3_reinstateToBoardMs: reinstateMs, sm3_limitMs: 5_000, auditRows: 2, stackStartupMs: startupMs(), clockSpeed: s.clockSpeed });
});
