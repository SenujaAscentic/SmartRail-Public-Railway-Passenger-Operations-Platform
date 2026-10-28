import { expect, test } from "@playwright/test";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { colomboDate, getJson, listen, openJourney, record, serverNow, signInAsController, stack, stationId, until } from "../support/helpers.ts";
import { repo } from "../support/stack.ts";
import { useOwnStack } from "../support/own-stack.ts";
import { bearer, staffToken } from "../support/staff.ts";

// N2: privacy of a controller's decision. No public surface shows who made it: no staff name, staff number or staff id.
//
// Given  the demo clock is about 09:32 on Wed 21 Oct; train 1002 (not started); the public stream is listened to
// When   the controller opens the delay form: under the note field it says "Visible to passengers – no names or personal details" (K-19)
// And    sets a delay of 8 minutes with a reason code and a note
// Then   the public journey API, the search, the departure and arrival boards and the public stream carry the reason and the delay
//        (the note is public by design, contract section 5), but no staff name, no staff number and no staff id (T-07, contract 4: staff ids
//        are removed from public events), and the station display page shows none either
//        (control: the controller's own audit log does carry the staff number and the staff id, so the scan can see them)

const startupMs = useOwnStack("2026-10-21T09:30:00+05:30");
const WARNING = "Visible to passengers – no names or personal details";
const NOTE = "Signal fault near Ragama";

test("N2: a delay with a note shows no staff name, staff number or staff id on any public surface", async ({ page, context }) => {
  const s = stack();
  const staff = (JSON.parse(readFileSync(join(repo, "shared", "seed", "staff.json"), "utf-8")) as { accounts: { staffNumber: string; name: string }[] }).accounts;

  // The controller signs in and opens train 1002; the public stream of that journey is listened to from now on.
  await signInAsController(page);
  const journeyId = await openJourney(page, "1002");
  const stream = listen(journeyId);

  // The controller opens the delay form: the warning is under the note field and is the note field's description.
  await page.getByRole("button", { name: "Delay", exact: true }).click();
  const form = page.getByRole("form", { name: "Set a delay" });
  await expect(form.getByText(WARNING)).toBeVisible();
  await expect(form.getByLabel(/^Note/)).toHaveAccessibleDescription(WARNING);
  await page.screenshot({ path: "../docs/qa/evidence/n2-delay-form-warning.png" });

  // The controller sets 8 minutes, a reason code and a note.
  await form.getByLabel("Delay in minutes").fill("8");
  await form.getByLabel("Reason code").selectOption("SignalFailure");
  await form.getByLabel(/^Note/).fill(NOTE);
  await form.getByRole("button", { name: "Set delay" }).click();
  await expect(page.getByText("Done. The change is on its way to everyone.")).toBeVisible();

  // The public stream announces the delay.
  await until("DelayChanged on the public stream", async () => stream.messages.find((m) => m.event === "DelayChanged" && m.data?.payload?.delayMinutes === 8), 20, 100);

  // The staff data to look for: every staff name and number of the seed, and the staff id of the controller (read from its audit log).
  const controller = await staffToken("CTRL-01");
  const auditText = await (await fetch(`${s.api}/api/v1/ops/audit?journeyId=${journeyId}`, { headers: bearer(controller) })).text();
  const actorId = /"actor":\s*\{[^}]*?"(?:id|accountId)":\s*"([0-9a-f-]{36})"/.exec(auditText)?.[1];
  expect(actorId, "the audit log names the actor by id").toBeTruthy();
  const needles = [...staff.flatMap((a) => [a.name, a.staffNumber]), actorId!];
  expect(auditText, "control: the audit log (staff only) shows the staff number").toContain("CTRL-01");

  // Everything public that mentions this train: journey, search, boards. (The platform display reads the board; the contract's
  // GET /stations/{id}/platforms/{platform}/next is not implemented, which is reported separately.)
  const fort = await stationId("FOT");
  const polgahawela = await stationId("PLG");
  const kandy = await stationId("KDT");
  const date = colomboDate(await serverNow());
  const api = `${s.api}/api/v1`;
  const publicJson: Record<string, string> = {
    journey: JSON.stringify(await getJson(`${api}/journeys/${journeyId}`)),
    search: JSON.stringify(await getJson(`${api}/journeys/search?from=${kandy}&to=${fort}&date=${date}&after=1970-01-01T00:00:00Z`)),
    boardPolgahawela: JSON.stringify(await getJson(`${api}/stations/${polgahawela}/board?kind=departures&limit=50`)),
    boardKandy: JSON.stringify(await getJson(`${api}/stations/${kandy}/board?kind=departures&limit=50`)),
    arrivalsPolgahawela: JSON.stringify(await getJson(`${api}/stations/${polgahawela}/board?kind=arrivals&limit=50`)),
    stream: JSON.stringify(stream.messages.map((m) => ({ event: m.event, data: m.data }))),
  };
  stream.stop();

  // The station display page of Polgahawela, as text.
  const display = await context.newPage();
  await display.goto(`${s.display}/board/PLG`);
  await expect(display.getByText("8 min late")).toBeVisible({ timeout: 30_000 });
  const displayText = await display.locator("body").innerText();
  await display.screenshot({ path: "../docs/qa/evidence/n2-board.png" });

  // The scan is not empty: the public journey does carry the delay and the (public) reason; and none of the staff data is in any surface.
  expect(publicJson.journey).toContain(`"SignalFailure"`);
  expect(publicJson.journey).toContain(NOTE);
  expect(publicJson.stream).toContain("DelayChanged");
  for (const [surface, text] of Object.entries({ ...publicJson, stationDisplay: displayText })) {
    for (const needle of needles) expect(text, `${surface} must not contain "${needle}"`).not.toContain(needle);
  }
  expect(publicJson.journey, "no staff source in the public journey").not.toContain(`"source"`);
  expect(publicJson.stream, "no staff source in the public stream").not.toContain(`"source"`);

  record("n2", { train: "1002", surfacesScanned: [...Object.keys(publicJson), "stationDisplay"], needles: needles.length, streamMessages: stream.messages.length, stackStartupMs: startupMs(), clockSpeed: s.clockSpeed });
});
