import { expect, test } from "@playwright/test";
import { colomboClock, colomboDate, getJson, listen, record, serverNow, signInAsController, stack, startSimulator, stationId, until } from "../support/helpers.ts";
import { killTree } from "../support/stack.ts";

// Flow 1: search finds the train; the cab tablet (the simulator; the driver app is deferred) drives it 6 minutes late; the live stream
// announces the delay and the ops web and the station board show "6 min late" within a minute (SM-2). The passenger app shows the same
// words from the same fixture (Flutter fixture tests).

test("flow 1: search finds the train, a 6 minute delay is announced and shown within a minute", async ({ page, context }) => {
  const s = stack();

  // ---- search (what the passenger app calls) ----
  const today = colomboDate(await serverNow());
  const found = await getJson<any[]>(`${s.api}/api/v1/journeys/search?from=${await stationId("FOT")}&to=${await stationId("KDT")}&date=${today}&limit=50`);
  const train = found.find((j) => j.serviceNumber === "2001");
  expect(train, "the search lists service 2001 (Colombo Fort to Kandy, 06:00)").toBeTruthy();
  expect(train.status.kind).toBe("NotStarted");
  const journeyId: string = train.journeyId;
  const stream = listen(journeyId);

  // ---- the cab tablet runs the train 6 minutes late ----
  const simulator = startSimulator({ service: "2001", device: "CAB-02", driver: "DRV-03", extra: ["--delay", "6"] });
  try {
    const departed = await until("the train leaving Colombo Fort", async () => stream.messages.find((m) => m.event === "StopStatusChanged" && m.data.payload?.to === "Departed"), 600, 500);
    const delayed = await until("DelayChanged with 6 minutes", async () => stream.messages.find((m) => m.event === "DelayChanged" && m.data.payload?.delayMinutes === 6), 60);
    const sm2_streamMs = delayed.at - departed.at;
    expect(sm2_streamMs, "the delay is announced within a minute of the departure that shows it").toBeLessThan(60_000);

    // ---- what people see ----
    const detail = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
    expect(detail.status).toMatchObject({ kind: "Late", delayMinutes: 6 });

    await signInAsController(page);
    const row = page.getByRole("row", { name: /2001/ }).filter({ hasText: "6 min late" }).first();
    await expect(row).toBeVisible({ timeout: 60_000 });
    const opsAt = Date.now();

    const display = await context.newPage();
    await display.goto(`${s.display}/board/GPH`);
    const gph = detail.stops.find((x: any) => x.name === "Gampaha");
    const shown = colomboClock(new Date(gph.scheduledDeparture));
    const boardRow = display.locator("li.line", { has: display.locator("time.line-time", { hasText: new RegExp(`^${shown}$`) }) });
    await expect(boardRow).toContainText("6 min late", { timeout: 60_000 });
    const boardAt = Date.now();
    await display.screenshot({ path: "../docs/qa/evidence/flow1-board-late.png" });
    await page.screenshot({ path: "../docs/qa/evidence/flow1-ops-late.png" });

    record("flow1", {
      found: "search listed 2001 as NotStarted before the run",
      sm2_departureToDelayEventMs: sm2_streamMs,
      sm2_delayEventToOpsWebMs: opsAt - delayed.at,
      sm2_delayEventToBoardMs: boardAt - delayed.at,
      sm2_limitMs: 60_000,
      clockSpeed: s.clockSpeed,
    });
  } finally {
    stream.stop();
    killTree(simulator.process.pid!);
  }
});
