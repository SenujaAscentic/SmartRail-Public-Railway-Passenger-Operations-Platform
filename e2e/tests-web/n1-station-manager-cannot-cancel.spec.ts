import { expect, test } from "@playwright/test";
import { getJson, listen, openJourney, record, signInAsController, stack } from "../support/helpers.ts";
import { useOwnStack } from "../support/own-stack.ts";
import { bearer, signInAs, staffToken } from "../support/staff.ts";

// N1: a station manager tries to cancel a train and is refused; the board and the audit log do not change.
//
// Given  the demo clock is about 09:52 on Wed 21 Oct; train 2002 (not started) is on the board of Polgahawela as "Not started" (S-09), its
//        journey has no audit rows, and station manager SM-PLG (Polgahawela is a stop of 2002) is signed in to the ops web
// Then   the station manager sees the journey but no Cancel button, only "Only the controller can make decisions on a journey." (R-17)
// When   the station manager sends the cancel request to the API anyway, with the right version
// Then   the answer is 403 (contract 3.2: cancel is for the Controller), and
//        the board row still says "Not started", the journey has the same status and version, no JourneyCancelled event reached the stream,
//        and the audit log still has no row for the journey

const startupMs = useOwnStack("2026-10-21T09:50:00+05:30");
const colombo = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", hour: "2-digit", minute: "2-digit", hour12: false });

test("N1: a station manager cannot cancel a train: 403, the board and the audit log do not change", async ({ page, context, browser }) => {
  const s = stack();

  // The controller signs in only to find the journey and to read the audit page afterwards.
  await signInAsController(page);
  const journeyId = await openJourney(page, "2002");
  const before = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  const plg = before.stops.find((x: any) => x.name === "Polgahawela");
  const timeOnBoard = colombo.format(new Date(plg.scheduledDeparture));

  // The board of Polgahawela shows the train as "Not started"; the stream is listened to from here on.
  const board = await context.newPage();
  await board.goto(`${s.display}/board/PLG`);
  const row = board.getByRole("listitem").filter({ has: board.getByText(timeOnBoard, { exact: true }) });
  await expect(row).toContainText("Not started", { timeout: 60_000 });
  const stream = listen(journeyId);

  // The audit log of the journey has no row yet.
  await page.getByRole("link", { name: "Audit" }).click();
  await page.getByLabel("Journey id").fill(journeyId);
  await page.getByRole("button", { name: "Apply" }).click();
  await expect(page.getByText("Nothing recorded for these filters.")).toBeVisible();

  // The station manager signs in (a browser of their own) and opens the journey: no decisions, only the explanation.
  const managerContext = await browser.newContext();
  const manager = await managerContext.newPage();
  await signInAs(manager, "SM-PLG");
  await manager.goto(`${s.opsWeb}/journeys/${journeyId}`);
  await expect(manager.getByText("Only the controller can make decisions on a journey.")).toBeVisible();
  await expect(manager.getByRole("button", { name: "Cancel journey" })).toHaveCount(0);
  await manager.screenshot({ path: "../docs/qa/evidence/n1-manager-no-decisions.png" });

  // The station manager sends the cancel request to the API anyway.
  const refused = await fetch(`${s.api}/api/v1/ops/journeys/${journeyId}/cancel`, {
    method: "POST",
    headers: bearer(await staffToken("SM-PLG")),
    body: JSON.stringify({ reasonCode: "TrainFault", note: null, expectedVersion: before.version }),
  });
  expect(refused.status, "contract 3.2: cancel is for the Controller only").toBe(403);

  // Nothing changed: the board, the journey, the stream and the audit log.
  await expect(row).toContainText("Not started");
  await expect(row).not.toContainText("Cancelled");
  const after = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  expect(after.status).toEqual(before.status);
  expect(after.version).toBe(before.version);
  expect(stream.messages.filter((m) => m.event === "JourneyCancelled")).toHaveLength(0);
  stream.stop();
  await page.getByRole("button", { name: "Apply" }).click();
  await expect(page.getByText("Nothing recorded for these filters.")).toBeVisible();
  await board.screenshot({ path: "../docs/qa/evidence/n1-board-unchanged.png" });
  await page.screenshot({ path: "../docs/qa/evidence/n1-audit-unchanged.png" });
  await managerContext.close();

  record("n1", { train: "2002", managerStatus: refused.status, versionBefore: before.version, versionAfter: after.version, auditRows: 0, stackStartupMs: startupMs(), clockSpeed: s.clockSpeed });
});
