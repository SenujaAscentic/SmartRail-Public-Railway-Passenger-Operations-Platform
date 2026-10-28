import { expect, test } from "@playwright/test";
import { colomboDate, getJson, listen, record, serverNow, signInAsController, stack, startSimulator, stationId, until } from "../support/helpers.ts";
import { killTree } from "../support/stack.ts";

// Flow 2: the cab tablet (the simulator; the driver app is deferred) starts train 1001 and drives it. The stream announces the start,
// the departure and the moving position, the journey becomes Running and Live, and the ops web shows the train ON the line.
// (The passenger app renders the same events from the same fixtures: Flutter fixture tests.)

test.setTimeout(900_000); // alone, the demo clock needs about 9 real minutes to reach 06:25

test("flow 2: the simulator starts a journey and the train moves: events, state and the line view", async ({ page }) => {
  const s = stack();
  const today = colomboDate(await serverNow());
  const found = await getJson<any[]>(`${s.api}/api/v1/journeys/search?from=${await stationId("FOT")}&to=${await stationId("KDT")}&date=${today}&limit=50`);
  const train = found.find((j) => j.serviceNumber === "1001");
  expect(train, "the search lists service 1001").toBeTruthy();
  expect(train.status.kind).toBe("NotStarted");
  const journeyId: string = train.journeyId;
  const stream = listen(journeyId);

  await signInAsController(page);
  const spawnedAt = Date.now();
  const simulator = startSimulator({ service: "1001", device: "CAB-01", driver: "DRV-01" });
  try {
    const started = await until("JourneyStarted", async () => stream.messages.find((m) => m.event === "JourneyStarted"), 480, 500);
    const departed = await until("the departure from Colombo Fort", async () => stream.messages.find((m) => m.event === "StopStatusChanged" && m.data.payload?.to === "Departed"), 480, 500);
    const moving = await until("three position updates", async () => {
      const positions = stream.messages.filter((m) => m.event === "TrainPositionUpdated");
      return positions.length >= 3 ? positions : undefined;
    }, 120);

    // The train really moves: the fraction of the way to the next station grows between two reads.
    const first = (await getJson(`${s.api}/api/v1/journeys/${journeyId}`)).freshness.lastPosition;
    await page.waitForTimeout(8_000);
    const detail = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
    const second = detail.freshness.lastPosition;
    expect(detail.freshness.tracking).toBe("Live");
    expect(second, "a position is announced").toBeTruthy();
    const progress = (p: any) => (p.lastStationId === first.lastStationId ? p.fractionToNext : 1 + p.fractionToNext);
    expect(progress(second), "the train is further along than 8 seconds ago").toBeGreaterThan(progress(first));
    expect(detail.stops[0].status).toBe("Departed");
    expect(detail.stops[0].actualDeparture).toBeTruthy();

    // The stream carries no staff field (T-07): none of these messages names a source, a device or a session.
    for (const m of stream.messages) expect(JSON.stringify(m.data)).not.toMatch(/"source"|deviceId|cabSession|crewSession/i);

    // The ops web shows the train on the line, in words and with its pin.
    const line = page.locator("section", { has: page.getByRole("heading", { name: "Line view" }) });
    await expect(line.locator(".track-pin", { hasText: "1001" })).toBeVisible({ timeout: 60_000 });
    await expect(line.getByText(/between .* and .*|at Colombo Fort/).first()).toBeVisible();
    await line.screenshot({ path: "../docs/qa/evidence/flow2-line-view.png" });

    record("flow2", {
      spawnToJourneyStartedMs: started.at - spawnedAt,
      startedToDepartureEventMs: departed.at - started.at,
      positionUpdatesSeen: moving.value.length,
      trackingAfterDeparture: detail.freshness.tracking,
      fractionBefore: progress(first),
      fractionAfter8s: progress(second),
      staffFieldsInPublicStream: 0,
      clockSpeed: s.clockSpeed,
    });
  } finally {
    stream.stop();
    killTree(simulator.process.pid!);
  }
});
