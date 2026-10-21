import { expect, test } from "@playwright/test";
import { colomboClock, getJson, openJourney, record, serverNow, signInAsController, stack, stationId, until } from "../support/helpers.ts";

// Flow 4: a platform change with less than 5 minutes of notice. Train 1001 leaves Colombo Fort at 06:30 on the demo clock; the change is
// made between 06:26 and 06:27:30, so the lead time (expected departure minus now) is under 5 minutes. The preview warns and
// recommends a hold; with a hold, the displays say "The train will wait for passengers"; without one the wording makes no promise (A-113).
// The old platform's display sends people to the new one. (The crew instruction is deferred.)

test.setTimeout(900_000); // alone, the demo clock needs about 6 real minutes to reach 06:26

test("flow 4: a platform change under 5 minutes with a hold: the warning, the displays, no promise without the hold", async ({ page, context }) => {
  const s = stack();
  const fort = await stationId("FOT");
  const platforms: { number: string; inService: boolean }[] = (await getJson(`${s.api}/api/v1/stations/${fort}`)).platforms;

  // ---- preparation (before the window): sign in, open the train, open the displays ----
  await signInAsController(page);
  const journeyId = await openJourney(page, "1001");
  const before = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  const oldPlatform: string = before.stops[0].platform;
  const newPlatform = platforms.find((p) => p.inService && p.number !== oldPlatform)!.number;
  const oldDisplay = await context.newPage();
  await oldDisplay.goto(`${s.display}/platform/FOT/${oldPlatform}`);
  const newDisplay = await context.newPage();
  await newDisplay.goto(`${s.display}/platform/FOT/${newPlatform}`);
  await expect(oldDisplay.getByLabel(`Platform ${oldPlatform}`)).toBeVisible();
  await page.bringToFront();

  // ---- wait for the window on the demo clock ----
  const clockAt = async () => colomboClock(await serverNow());
  const inWindow = async () => {
    const now = await serverNow();
    const text = colomboClock(now);
    const seconds = Number(new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", second: "2-digit" }).format(now));
    if (text > "06:27" || (text === "06:27" && seconds > 30)) throw new Error(`The demo clock is already ${text}: the window (06:26 to 06:27:30) has passed. Run this flow alone, or earlier in the run.`);
    return text >= "06:26" ? text : undefined;
  };
  await until("the demo clock to reach 06:26", async () => inWindow(), 900, 1000);

  // ---- the change, with a hold ----
  await page.getByRole("button", { name: "Change platform", exact: true }).click();
  const dialog = page.getByRole("dialog", { name: "Change platform" });
  await dialog.getByRole("button", { name: new RegExp(`^Platform ${newPlatform}$`) }).click();
  const lead = dialog.getByRole("region", { name: "Lead time" });
  await expect(lead).toContainText("Late");
  await expect(lead).toContainText("under 5 min");
  const minutes = Number(/= (-?\d+) min/.exec(await lead.innerText())![1]);
  expect(minutes, "the lead time is under 5 minutes").toBeLessThan(5);
  await expect(dialog.getByLabel(/Set a hold at the new platform \(recommended\)/)).toBeVisible();   // recommended, not forced

  const preview = dialog.getByLabel("What people will see");
  await expect(preview).toContainText(`Now departing from Platform ${newPlatform}`);                   // without a hold: no promise
  await expect(preview).not.toContainText("will wait");
  await dialog.getByLabel(/Set a hold at the new platform/).check();
  await expect(preview).toContainText(`The train will wait for passengers at Platform ${newPlatform}`); // with a hold: the promise
  await page.screenshot({ path: "../docs/qa/evidence/flow4-dialog.png" });
  const clickedAt = Date.now();
  await dialog.getByRole("button", { name: "Confirm change and hold" }).click();

  // ---- SM-3: the displays ----
  const promise = newDisplay.getByText(`The train will wait for passengers at Platform ${newPlatform}`).first();
  await expect(promise).toBeVisible({ timeout: 15_000 });
  const sm3New = Date.now() - clickedAt;
  const redirect = oldDisplay.getByText(new RegExp(`now departs from Platform ${newPlatform}`));
  await expect(redirect).toBeVisible({ timeout: 15_000 });
  const sm3Old = Date.now() - clickedAt;
  expect(Math.max(sm3New, sm3Old), "SM-3: both displays within 5 s").toBeLessThan(5_000);
  await newDisplay.screenshot({ path: "../docs/qa/evidence/flow4-new-platform-display.png" });
  await oldDisplay.screenshot({ path: "../docs/qa/evidence/flow4-old-platform-display.png" });

  const changed = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  expect(changed.stops[0]).toMatchObject({ platform: newPlatform, previousPlatform: oldPlatform });
  expect(changed.hold).toMatchObject({ stationId: fort });

  // ---- release the hold: the promise goes ----
  await page.getByRole("button", { name: "Release the hold" }).click();
  await until("the hold to be released", async () => (await getJson(`${s.api}/api/v1/journeys/${journeyId}`)).hold === null, 30);
  await expect(newDisplay.getByText("will wait for passengers")).toHaveCount(0, { timeout: 15_000 });
  await expect(oldDisplay.getByText(`Now departing from Platform ${newPlatform}`)).toBeVisible({ timeout: 15_000 });   // S-20 on the old platform: where it goes, no promise

  record("flow4", {
    clockAtChange: await clockAt(),
    leadTimeMinutes: minutes,
    sm3_confirmToNewPlatformDisplayMs: sm3New,
    sm3_confirmToOldPlatformDisplayMs: sm3Old,
    sm3_limitMs: 5_000,
    holdReleased: true,
    platformChange: `${oldPlatform} -> ${newPlatform}`,
    clockSpeed: s.clockSpeed,
  });
});
