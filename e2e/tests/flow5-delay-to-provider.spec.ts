import { expect, test } from "@playwright/test";
import { spawnSync } from "node:child_process";
import { getJson, openJourney, record, signInAsController, stack, until } from "../support/helpers.ts";
import { apiClockNow, killTree, startRailBites } from "../support/stack.ts";
import { writeFileSync, readFileSync } from "node:fs";
import { stateFile } from "../support/stack.ts";

// Flow 5: a train is delayed in the ops web and the catering provider (RailCantima, code name RailBites) receives the new ETA as a
// signed webhook within 10 s (SM-5). Then the provider is stopped, the train is delayed again, the provider comes back: the webhook is
// delivered after a retry, exactly once.

type ProviderEvent = { eventId: string; type: string; serviceNumber: string; stationRef: string; expectedArrival: string | null; scheduledArrival: string | null };

const events = async () => getJson<ProviderEvent[]>(`${stack().railBites}/events.json`);
const etasFor = (all: ProviderEvent[], service: string) => all.filter((e) => e.serviceNumber === service && e.type === "provider.eta-updated");

async function setDelay(page: import("@playwright/test").Page, minutes: number) {
  await page.getByRole("button", { name: "Delay", exact: true }).click();
  const form = page.getByRole("form", { name: "Set a delay" });
  await form.getByLabel("Delay in minutes").fill(String(minutes));
  await form.getByLabel("Reason code").selectOption("TrainFault");
  await form.getByLabel(/Note/).fill("Engine failure (E2E flow 5)");
  const clickedAt = Date.now();
  await form.getByRole("button", { name: "Set delay" }).click();
  return clickedAt;
}

test("flow 5: a delay reaches the provider within 10 s; after a provider outage it is delivered once", async ({ page }) => {
  const s = stack();
  await signInAsController(page);
  await openJourney(page, "1001");

  // ---- the delay, and the provider hears of it (SM-5 < 10 s) ----
  const clickedAt = await setDelay(page, 15);
  await expect(page.getByText("Done. The change is on its way to everyone.")).toBeVisible();
  const first = await until("the ETA events at the provider", async () => {
    const found = etasFor(await events(), "1001");
    return found.length >= 2 ? found : undefined;
  }, 30);
  const sm5 = first.at - clickedAt;
  expect(sm5, `SM-5: the provider must hear within 10 s (took ${sm5} ms)`).toBeLessThan(10_000);
  expect(first.value.map((e) => e.stationRef).sort(), "one ETA for each catering station, and no other").toEqual(["PDA", "PLG"]);
  for (const e of first.value) {
    expect((Date.parse(e.expectedArrival!) - Date.parse(e.scheduledArrival!)) / 60_000, "the ETA is the scheduled time plus 15 minutes").toBe(15);
  }

  // what a person at the provider sees
  const providerPage = await page.context().newPage();
  await providerPage.goto(`${s.railBites}/events`);
  await expect(providerPage.getByRole("heading", { name: /RailCantima/ })).toBeVisible();
  await expect(providerPage.getByText("ETA updated").first()).toBeVisible();
  await providerPage.screenshot({ path: "../docs/qa/evidence/flow5-provider-events.png", fullPage: true });
  await providerPage.close();

  // ---- provider stopped, train delayed again, provider back: delivered after a retry, no duplicate ----
  const state = JSON.parse(readFileSync(stateFile, "utf-8")) as { pids: Record<string, number>; catering: { secret: string; key: string } };
  killTree(state.pids.railBites!);
  await expect.poll(async () => fetch(`${s.railBites}/events`).then(() => "up", () => "down"), { timeout: 15_000 }).toBe("down");

  const before = etasFor(await events().catch(() => []), "1001").length; // unreachable now: 0
  const secondClick = await setDelay(page, 25);
  await expect(page.getByText("Done. The change is on its way to everyone.")).toBeVisible();
  await page.waitForTimeout(8_000); // SmartRail tries, fails and keeps the webhook (the retry waits grow: 10 s, 30 s, 1 min of the demo clock)

  const clock = await apiClockNow(s.api);
  const restarted = startRailBites(state.catering, clock);
  state.pids.railBites = restarted.pid!;
  writeFileSync(stateFile, JSON.stringify({ ...JSON.parse(readFileSync(stateFile, "utf-8")), pids: state.pids }, null, 2));
  const restartedAt = Date.now();
  await until("RailBites to be back", async () => (await fetch(`${s.railBites}/events`)).ok, 120);

  const delivered = await until("the retried ETA events at the provider", async () => {
    const found = etasFor(await events(), "1001").filter((e) => Date.parse(e.expectedArrival!) - Date.parse(e.scheduledArrival!) === 25 * 60_000);
    return found.length >= 2 ? found : undefined;
  }, 120);
  const all = etasFor(await events(), "1001");
  const ids = all.map((e) => e.eventId);
  expect(new Set(ids).size, "no event is stored twice").toBe(ids.length);
  expect(all.filter((e) => Date.parse(e.expectedArrival!) - Date.parse(e.scheduledArrival!) === 25 * 60_000), "each station got the new ETA exactly once").toHaveLength(2);
  expect(before).toBe(0);

  record("flow5", {
    sm5_delayToProviderMs: sm5,
    sm5_limitMs: 10_000,
    outage_delayClickToDeliveredMs: delivered.at - secondClick,
    outage_providerBackToDeliveredMs: delivered.at - restartedAt,
    etaEventsStored: ids.length,
    duplicates: ids.length - new Set(ids).size,
    clockSpeed: s.clockSpeed,
  });
});
