import { expect, test } from "@playwright/test";
import { getJson, openJourney, record, signInAsController, stack } from "../support/helpers.ts";
import { useOwnStack } from "../support/own-stack.ts";

// W1: the controller delays a train with a reason in the ops web; the station board and the platform display both say "8 min late".
//
// Given  the demo clock is 09:30 on Wed 21 Oct; train 1002 (Kandy to Colombo Fort, 10:00, not started) is on the board and the platform
//        display of Polgahawela, with no delay
// When   the controller sets a delay of 8 minutes with the reason "Signal failure" (US-O4)
// Then   the board row and the platform display say "8 min late" (S-03; US-D1, US-D2), and the controller's own page says it was done
// The two latencies (click to text visible) go to docs/qa/evidence/w1.json. SM-3 limit: 5 s.

const startupMs = useOwnStack("2026-10-21T09:30:00+05:30"); // 1002 leaves Kandy at 10:00; the stack takes a few real minutes of demo clock to come up
const colombo = new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", hour: "2-digit", minute: "2-digit", hour12: false });

test("W1: a delay set in the ops web shows as '8 min late' on the board and the platform display", async ({ page, context }) => {
  const s = stack();
  const minutes = 8; // S-03: "8 min late"

  // The controller signs in and opens train 1002.
  await signInAsController(page);
  const journeyId = await openJourney(page, "1002");

  // The train's stop at Polgahawela: its scheduled time and platform tell us which board row and which platform display to watch.
  const detail = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  const plg = detail.stops.find((x: any) => x.name === "Polgahawela");
  const timeOnBoard = colombo.format(new Date(plg.scheduledDeparture));

  // The board of Polgahawela shows the train, and no delay yet.
  const board = await context.newPage();
  await board.goto(`${s.display}/board/PLG`);
  const row = board.getByRole("listitem").filter({ has: board.getByText(timeOnBoard, { exact: true }) });
  await expect(row, "the train is on the board before the delay").toBeVisible({ timeout: 60_000 });
  await expect(row).not.toContainText("min late");

  // The platform display of its Polgahawela platform shows it as the next train, and no delay yet.
  const platform = await context.newPage();
  await platform.goto(`${s.display}/platform/PLG/${plg.platform}`);
  const next = platform.getByRole("region", { name: "Next" }).filter({ hasText: timeOnBoard });
  await expect(next, "the train is the next one on its platform display").toBeVisible({ timeout: 60_000 });
  await expect(next).not.toContainText("min late");

  // The controller opens the delay form, enters 8 minutes and a reason code, and sets it.
  await page.bringToFront();
  await page.getByRole("button", { name: "Delay", exact: true }).click();
  const form = page.getByRole("form", { name: "Set a delay" });
  await form.getByLabel("Delay in minutes").fill(String(minutes));
  await form.getByLabel("Reason code").selectOption("SignalFailure");
  const clickedAt = Date.now();
  await form.getByRole("button", { name: "Set delay" }).click();

  // The board row and the platform display both say "8 min late"; each latency is measured from the click.
  const [boardMs, platformMs] = await Promise.all([
    expect(row).toContainText(`${minutes} min late`, { timeout: 15_000 }).then(() => Date.now() - clickedAt),
    expect(next).toContainText(`${minutes} min late`, { timeout: 15_000 }).then(() => Date.now() - clickedAt),
  ]);
  await expect(page.getByText("Done. The change is on its way to everyone.")).toBeVisible();
  expect(Math.max(boardMs, platformMs), "SM-3: both screens within 5 s").toBeLessThan(5_000);
  await board.screenshot({ path: "../docs/qa/evidence/w1-board-late.png" });
  await platform.screenshot({ path: "../docs/qa/evidence/w1-platform-late.png" });

  // The API agrees (the screens only display what the backend sent).
  const after = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  expect(after.status).toMatchObject({ kind: "Late", delayMinutes: minutes });
  expect(after.reason.code).toBe("SignalFailure");

  record("w1", { train: "1002", station: "Polgahawela", delayMinutes: minutes, w1_clickToBoardMs: boardMs, w1_clickToPlatformDisplayMs: platformMs, sm3_limitMs: 5_000, stackStartupMs: startupMs(), clockSpeed: s.clockSpeed });
});
