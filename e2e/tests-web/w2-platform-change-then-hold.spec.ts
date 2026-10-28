import { expect, test } from "@playwright/test";
import { getJson, openJourney, record, serverNow, signInAsController, stack, stationId, until } from "../support/helpers.ts";
import { useOwnStack } from "../support/own-stack.ts";

// W2: a platform change with less than 5 minutes of notice, then a hold. Nothing promises that the train waits until the hold is real.
//
// Given  the demo clock runs at 2x on Wed 21 Oct, a few minutes before 06:30; train 1001 (Colombo Fort to Kandy, 06:30 from Platform 5) has not started;
//        the platform displays of Platform 5 (old) and of another in-service platform (new) are open
// When   the controller changes the platform with under 5 minutes of notice (R-11), WITHOUT a hold
// Then   the old display says "Train 1001 to Kandy now departs from Platform N" with "Now departing from Platform N" (US-D2 AC3, S-20),
//        the new display shows the train as its next one, and no display says "will wait" (R-11, A-113)
// When   the controller then sets a hold at Colombo Fort (R-22)
// Then   the new display says "The train will wait for passengers at Platform N" (S-19)
// Latencies (click to text visible) go to docs/qa/evidence/w2.json. SM-3 limit: 5 s.

// The window for "under 5 minutes" is 06:25 to 06:30 on the demo clock, which is 2.5 real minutes at speed 2. The test does not rely on the
// start: it reads the train's expected departure from the API and waits until the demo clock is 4 minutes before it. The start 06:24 only keeps
// that wait short (about 40 real seconds); the stack may take up to about 2.5 real minutes to come up and the test still finds the window.
const startupMs = useOwnStack("2026-10-21T06:24:00+05:30", 2);

test("W2: a platform change under 5 minutes, then a hold: 'will wait' appears only with the hold", async ({ page, context }) => {
  const s = stack();
  const fort = await stationId("FOT");
  const platforms: { number: string; inService: boolean }[] = (await getJson(`${s.api}/api/v1/stations/${fort}`)).platforms;

  // The controller signs in and opens train 1001; the old and the new platform are read from the API, not assumed.
  await signInAsController(page);
  const journeyId = await openJourney(page, "1001");
  const before = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  const oldPlatform: string = before.stops[0].platform;
  const newPlatform = platforms.find((p) => p.inService && p.number !== oldPlatform)!.number;

  // Both platform displays are open before anything changes.
  const oldDisplay = await context.newPage();
  await oldDisplay.goto(`${s.display}/platform/FOT/${oldPlatform}`);
  const newDisplay = await context.newPage();
  await newDisplay.goto(`${s.display}/platform/FOT/${newPlatform}`);
  await expect(oldDisplay.getByLabel(`Platform ${oldPlatform}`)).toBeVisible();
  await expect(newDisplay.getByLabel(`Platform ${newPlatform}`)).toBeVisible();
  await page.bringToFront();

  // Read the expected departure from the API (1001 starts here, so R-11 measures from its departure, there is no arrival) and wait, on the
  // API's demo clock and without a fixed sleep, until the train is 4 minutes from leaving; fail clearly if the window has gone.
  const expectedDeparture = Date.parse(before.stops[0].expectedDeparture);
  const leadSeconds = async () => (expectedDeparture - (await serverNow()).getTime()) / 1000;
  await until("the demo clock to be 4 minutes before the departure", async () => {
    const lead = await leadSeconds();
    if (lead < 30) throw new Error(`The window has passed: ${Math.round(lead)} s left on the demo clock. Start the demo clock earlier in this file.`);
    return lead <= 240 ? true : undefined;
  }, 280, 1000);

  // The controller opens the platform dialog and picks the new platform; it warns (Late, under 5 min) and recommends a hold, but does not force one.
  await page.getByRole("button", { name: "Change platform", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Change platform" });
  await dialog.getByRole("button", { name: new RegExp(`^Platform ${newPlatform}$`) }).click();
  const lead = dialog.getByRole("region", { name: "Lead time" });
  await expect(lead).toContainText("Late");
  await expect(lead).toContainText("under 5 min");
  const hold = dialog.getByLabel(/Set a hold at the new platform \(recommended\)/);
  await expect(hold).not.toBeChecked();

  // The preview makes no promise to wait, because no hold is ticked.
  const preview = dialog.getByLabel("What people will see");
  await expect(preview).toContainText(`Now departing from Platform ${newPlatform}`);
  await expect(preview).not.toContainText("will wait");
  await page.screenshot({ path: "../docs/qa/evidence/w2-dialog-no-hold.png" });

  // The controller confirms the change WITHOUT a hold.
  const changedAt = Date.now();
  await dialog.getByRole("button", { name: "Confirm platform change" }).click();

  // The old display sends people to the new platform, and the new display shows the train as its next one.
  const redirect = oldDisplay.getByText(new RegExp(`Train 1001 to Kandy now departs from Platform ${newPlatform}`));
  const arriving = newDisplay.getByRole("region", { name: "Next" }).filter({ hasText: "Kandy" });
  const [oldMs, newMs] = await Promise.all([
    expect(redirect).toBeVisible({ timeout: 15_000 }).then(() => Date.now() - changedAt),
    expect(arriving).toBeVisible({ timeout: 15_000 }).then(() => Date.now() - changedAt),
  ]);
  expect(Math.max(oldMs, newMs), "SM-3: both displays within 5 s").toBeLessThan(5_000);
  await expect(oldDisplay.getByText(`Now departing from Platform ${newPlatform}`)).toBeVisible();   // S-20: where it goes, no promise

  // Before the hold: no screen says the train will wait.
  await expect(oldDisplay.getByText("will wait")).toHaveCount(0);
  await expect(newDisplay.getByText("will wait")).toHaveCount(0);
  await oldDisplay.screenshot({ path: "../docs/qa/evidence/w2-old-display-no-hold.png" });
  const noHold = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  expect(noHold.hold, "no hold is set yet").toBeNull();
  expect(noHold.stops[0]).toMatchObject({ platform: newPlatform, previousPlatform: oldPlatform });

  // The controller now sets a hold at Colombo Fort for 3 minutes.
  await page.getByRole("button", { name: "Hold", exact: true }).click();
  const form = page.getByRole("form", { name: "Hold the train" });
  await form.getByLabel("Planned length in minutes").fill("3");
  const heldAt = Date.now();
  await form.getByRole("button", { name: "Hold train" }).click();

  // With a real hold, the new display says the train will wait (S-19).
  const promise = newDisplay.getByText(`The train will wait for passengers at Platform ${newPlatform}`).first();
  await expect(promise).toBeVisible({ timeout: 15_000 });
  const holdMs = Date.now() - heldAt;
  expect(holdMs, "SM-3: the promise within 5 s of the hold").toBeLessThan(5_000);
  await newDisplay.screenshot({ path: "../docs/qa/evidence/w2-new-display-hold.png" });
  expect((await getJson(`${s.api}/api/v1/journeys/${journeyId}`)).hold).toMatchObject({ stationId: fort });

  record("w2", { train: "1001", platformChange: `${oldPlatform} -> ${newPlatform}`, w2_changeToOldDisplayMs: oldMs, w2_changeToNewDisplayMs: newMs, w2_holdToWillWaitMs: holdMs, sm3_limitMs: 5_000, stackStartupMs: startupMs(), clockSpeed: s.clockSpeed });
});
