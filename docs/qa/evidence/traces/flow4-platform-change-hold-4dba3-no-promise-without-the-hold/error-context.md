# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: flow4-platform-change-hold.spec.ts >> flow 4: a platform change under 5 minutes with a hold: the warning, the displays, no promise without the hold
- Location: tests\flow4-platform-change-hold.spec.ts:11:1

# Error details

```
Error: expect(locator).toBeVisible() failed

Locator: getByText('Now departing from Platform 1').first()
Expected: visible
Timeout: 15000ms
Error: element(s) not found

Call log:
  - Expect "toBeVisible" getByText('Now departing from Platform 1').first() with timeout 15000ms
  - waiting for getByText('Now departing from Platform 1').first()

```

```yaml
- banner:
  - heading "Colombo Fort Towards Kandy" [level=1]
  - text: Platform 1
  - status: Live
  - time: 06:26
- main:
  - region "Next":
    - text: Next
    - time: 06:30
    - text: Kandy Express Not started Scheduled
    - time: 06:30
    - text: Calling at Maradana · Gampaha · Polgahawela · Peradeniya · Kandy
```

# Test source

```ts
  1  | import { expect, test } from "@playwright/test";
  2  | import { colomboClock, getJson, openJourney, record, serverNow, signInAsController, stack, stationId, until } from "../support/helpers.ts";
  3  | 
  4  | // Flow 4: a platform change with less than 5 minutes of notice. Train 1001 leaves Colombo Fort at 06:30 on the demo clock; the change is
  5  | // made between 06:26 and 06:27:30, so the lead time (expected departure minus now) is under 5 minutes. The preview warns and
  6  | // recommends a hold; with a hold, the displays say "The train will wait for passengers"; without one the wording makes no promise (A-113).
  7  | // The old platform's display sends people to the new one. (The crew instruction is deferred.)
  8  | 
  9  | test.setTimeout(900_000); // alone, the demo clock needs about 6 real minutes to reach 06:26
  10 | 
  11 | test("flow 4: a platform change under 5 minutes with a hold: the warning, the displays, no promise without the hold", async ({ page, context }) => {
  12 |   const s = stack();
  13 |   const fort = await stationId("FOT");
  14 |   const platforms: { number: string; inService: boolean }[] = (await getJson(`${s.api}/api/v1/stations/${fort}`)).platforms;
  15 | 
  16 |   // ---- preparation (before the window): sign in, open the train, open the displays ----
  17 |   await signInAsController(page);
  18 |   const journeyId = await openJourney(page, "1001");
  19 |   const before = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  20 |   const oldPlatform: string = before.stops[0].platform;
  21 |   const newPlatform = platforms.find((p) => p.inService && p.number !== oldPlatform)!.number;
  22 |   const oldDisplay = await context.newPage();
  23 |   await oldDisplay.goto(`${s.display}/platform/FOT/${oldPlatform}`);
  24 |   const newDisplay = await context.newPage();
  25 |   await newDisplay.goto(`${s.display}/platform/FOT/${newPlatform}`);
  26 |   await expect(oldDisplay.getByLabel(`Platform ${oldPlatform}`)).toBeVisible();
  27 |   await page.bringToFront();
  28 | 
  29 |   // ---- wait for the window on the demo clock ----
  30 |   const clockAt = async () => colomboClock(await serverNow());
  31 |   const inWindow = async () => {
  32 |     const now = await serverNow();
  33 |     const text = colomboClock(now);
  34 |     const seconds = Number(new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Colombo", second: "2-digit" }).format(now));
  35 |     if (text > "06:27" || (text === "06:27" && seconds > 30)) throw new Error(`The demo clock is already ${text}: the window (06:26 to 06:27:30) has passed. Run this flow alone, or earlier in the run.`);
  36 |     return text >= "06:26" ? text : undefined;
  37 |   };
  38 |   await until("the demo clock to reach 06:26", async () => inWindow(), 900, 1000);
  39 | 
  40 |   // ---- the change, with a hold ----
  41 |   await page.getByRole("button", { name: "Change platform", exact: true }).click();
  42 |   const dialog = page.getByRole("dialog", { name: "Change platform" });
  43 |   await dialog.getByRole("button", { name: new RegExp(`^Platform ${newPlatform}$`) }).click();
  44 |   const lead = dialog.getByRole("region", { name: "Lead time" });
  45 |   await expect(lead).toContainText("Late");
  46 |   await expect(lead).toContainText("under 5 min");
  47 |   const minutes = Number(/= (-?\d+) min/.exec(await lead.innerText())![1]);
  48 |   expect(minutes, "the lead time is under 5 minutes").toBeLessThan(5);
  49 |   await expect(dialog.getByLabel(/Set a hold at the new platform \(recommended\)/)).toBeVisible();   // recommended, not forced
  50 | 
  51 |   const preview = dialog.getByLabel("What people will see");
  52 |   await expect(preview).toContainText(`Now departing from Platform ${newPlatform}`);                   // without a hold: no promise
  53 |   await expect(preview).not.toContainText("will wait");
  54 |   await dialog.getByLabel(/Set a hold at the new platform/).check();
  55 |   await expect(preview).toContainText(`The train will wait for passengers at Platform ${newPlatform}`); // with a hold: the promise
  56 |   await page.screenshot({ path: "../docs/qa/evidence/flow4-dialog.png" });
  57 |   const clickedAt = Date.now();
  58 |   await dialog.getByRole("button", { name: "Confirm change and hold" }).click();
  59 | 
  60 |   // ---- SM-3: the displays ----
  61 |   const promise = newDisplay.getByText(`The train will wait for passengers at Platform ${newPlatform}`).first();
  62 |   await expect(promise).toBeVisible({ timeout: 15_000 });
  63 |   const sm3New = Date.now() - clickedAt;
  64 |   const redirect = oldDisplay.getByText(new RegExp(`now departs from Platform ${newPlatform}`));
  65 |   await expect(redirect).toBeVisible({ timeout: 15_000 });
  66 |   const sm3Old = Date.now() - clickedAt;
  67 |   expect(Math.max(sm3New, sm3Old), "SM-3: both displays within 5 s").toBeLessThan(5_000);
  68 |   await newDisplay.screenshot({ path: "../docs/qa/evidence/flow4-new-platform-display.png" });
  69 |   await oldDisplay.screenshot({ path: "../docs/qa/evidence/flow4-old-platform-display.png" });
  70 | 
  71 |   const changed = await getJson(`${s.api}/api/v1/journeys/${journeyId}`);
  72 |   expect(changed.stops[0]).toMatchObject({ platform: newPlatform, previousPlatform: oldPlatform });
  73 |   expect(changed.hold).toMatchObject({ stationId: fort });
  74 | 
  75 |   // ---- release the hold: the promise goes ----
  76 |   await page.getByRole("button", { name: "Release the hold" }).click();
  77 |   await until("the hold to be released", async () => (await getJson(`${s.api}/api/v1/journeys/${journeyId}`)).hold === null, 30);
  78 |   await expect(newDisplay.getByText("will wait for passengers")).toHaveCount(0, { timeout: 15_000 });
> 79 |   await expect(newDisplay.getByText(`Now departing from Platform ${newPlatform}`).first()).toBeVisible();    // S-20: where, no promise
     |                                                                                            ^ Error: expect(locator).toBeVisible() failed
  80 | 
  81 |   record("flow4", {
  82 |     clockAtChange: await clockAt(),
  83 |     leadTimeMinutes: minutes,
  84 |     sm3_confirmToNewPlatformDisplayMs: sm3New,
  85 |     sm3_confirmToOldPlatformDisplayMs: sm3Old,
  86 |     sm3_limitMs: 5_000,
  87 |     holdReleased: true,
  88 |     platformChange: `${oldPlatform} -> ${newPlatform}`,
  89 |     clockSpeed: s.clockSpeed,
  90 |   });
  91 | });
  92 | 
```