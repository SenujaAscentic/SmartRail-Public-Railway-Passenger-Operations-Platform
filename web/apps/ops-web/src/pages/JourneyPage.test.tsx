import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { ME, REFRESH, fakeBackend, json, renderApp } from "../test/fake-server.tsx";
import { RAMBUKKANA, journey, snapshot } from "../test/fixtures.ts";
import { frame, openSse, sse } from "../test/sse.ts";

const ID = "0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01";
const BASE = `/api/v1/ops/journeys/${ID}`;
const PLATFORM = `POST ${BASE}/stops/${RAMBUKKANA}/platform`;
const PREVIEW = `POST ${BASE}/stops/${RAMBUKKANA}/platform/preview`;

function backendFor(detail = journey(), delaySource: string | null = "Crew") {
  const backend = fakeBackend();
  backend.routes[REFRESH] = () => backend.tokenResponse("t");
  backend.stream.responder = sse(frame("snapshot", snapshot(detail, delaySource), 10), { hold: true });
  backend.routes[`GET /api/v1/stations/${RAMBUKKANA}`] = () =>
    json(200, { stationId: RAMBUKKANA, code: "RBK", name: "Rambukkana", latitude: 7.2, longitude: 80.4, cateringStation: false, platforms: [{ number: "1", inService: true }, { number: "2", inService: true }, { number: "4", inService: true }, { number: "5", inService: false }] });
  return backend;
}

const preview = (level: string, minutes: number, extra: Record<string, unknown> = {}) =>
  json(200, { leadTimeMinutes: minutes, leadTimeLevel: level, holdRecommended: level === "Late", conflicts: [], ...extra });

async function openPlatformDialog(user: ReturnType<typeof userEvent.setup>, platform = "4") {
  await user.click(await screen.findByRole("button", { name: "Change platform" }, { timeout: 3000 }));
  const form = await screen.findByRole("form", { name: "Change platform" });
  await user.click(await within(form).findByRole("button", { name: `Platform ${platform}` }));
  return form;
}

describe("the journey page", () => {
  it("shows the state, where each value came from, and the stops with their platforms", async () => {
    const backend = backendFor(journey({ status: { kind: "Late", delayMinutes: 6 }, stops: journey().stops.map((s) => (s.stationId === RAMBUKKANA ? { ...s, platform: "4", previousPlatform: "2" } : s)) }));

    renderApp(backend.services, `/journeys/${ID}`);

    expect(await screen.findByRole("heading", { level: 1, name: /1001 Kandy Express/ }, { timeout: 3000 })).toBeTruthy();
    expect(screen.getAllByText("6 min late").length).toBeGreaterThan(0);
    expect(screen.getAllByText(/Driver/).length).toBeGreaterThan(0);
    expect(screen.getByText("Platform 4 (was 2)")).toBeTruthy();
  });

  it("a station manager sees the journey but no decisions", async () => {
    const backend = backendFor();
    backend.routes[ME] = () => json(200, { name: "Demo Manager", role: "StationManager", stationId: RAMBUKKANA });

    renderApp(backend.services, `/journeys/${ID}`);

    expect(await screen.findByText("Only the controller can make decisions on a journey.", {}, { timeout: 3000 })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Delay" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Change platform" })).toBeNull();
  });

  it("tells the controller under every note field that the note is visible to passengers (K-19)", async () => {
    const backend = backendFor();
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Delay" }, { timeout: 3000 }));
    expect(screen.getByLabelText(/^Note/).getAttribute("aria-describedby")).toBe("delay-note-warning");
    expect(screen.getByText("Visible to passengers – no names or personal details")).toBeTruthy();

    await user.click(screen.getByRole("button", { name: "Close" }));
    await user.click(screen.getByRole("button", { name: "Cancel journey" }));
    const dialog = await screen.findByRole("dialog", { name: "Cancel this journey?" });
    expect(within(dialog).getByText("Visible to passengers – no names or personal details")).toBeTruthy();
  });

  it("shows who changed the journey first when a decision is refused with 409, and reloads it on request", async () => {
    const backend = backendFor();
    backend.routes[`POST ${BASE}/delay`] = () =>
      json(409, { code: "VERSION_CONFLICT", title: "Journey was changed by someone else", status: 409, detail: "Changed by R. Perera 4 s ago", currentVersion: 4, journey: journey({ version: 4, status: { kind: "Late", delayMinutes: 12 } }) });
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Delay" }, { timeout: 3000 }));
    await user.type(screen.getByLabelText("Delay in minutes"), "9");
    await user.selectOptions(screen.getByLabelText("Reason code"), "Weather");
    await user.click(screen.getByRole("button", { name: "Set delay" }));

    expect(await screen.findByText(/This train changed – review and try again\./)).toBeTruthy();
    expect(screen.getByText(/Changed by R\. Perera 4 s ago/)).toBeTruthy();
    expect(screen.getByText("Version 3")).toBeTruthy();
    await user.click(screen.getByRole("button", { name: "Reload" }));
    expect(screen.getByText("Version 4")).toBeTruthy();
    expect(screen.getAllByText("12 min late").length).toBeGreaterThan(0);
  });

  it("keeps a decision dialog as it is when a stream event arrives while it is open (rehearsal bug #6)", async () => {
    const detail = journey({ status: { kind: "Late", delayMinutes: 6 } });
    const backend = backendFor(detail);
    const stream = openSse(frame("snapshot", snapshot(detail, "Crew"), 10));
    backend.stream.responder = stream.responder;
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Terminate early" }, { timeout: 3000 }));
    const reason = screen.getByLabelText("Reason code");
    const station = screen.getByLabelText("The journey ends at");
    await user.selectOptions(reason, "Weather");
    reason.focus();

    // The train changes: a newer snapshot, with another version and the first stop already left.
    const newer = journey({ version: 4, status: { kind: "Late", delayMinutes: 9 }, stops: detail.stops.map((s, i) => (i === 0 ? { ...s, status: "Departed" } : s)) });
    stream.push(frame("snapshot", snapshot(newer, "Crew", 11), 11));
    await waitFor(() => expect(screen.getByText("Version 4")).toBeTruthy());

    expect(screen.getByRole("dialog")).toBeTruthy();                          // still open
    expect(screen.getByLabelText("Reason code")).toBe(reason);                 // the same element: not remounted
    expect(screen.getByLabelText("The journey ends at")).toBe(station);
    expect((reason as HTMLSelectElement).value).toBe("Weather");               // what was chosen is kept
    expect(document.activeElement).toBe(reason);                               // focus stays: an open <select> would close otherwise
  });

  it("sends the delay with its reason and the journey's version, and shows the answer", async () => {
    const backend = backendFor();
    let body: unknown;
    backend.routes[`POST ${BASE}/delay`] = () => json(200, journey({ version: 4, status: { kind: "Late", delayMinutes: 9 } }));
    const original = backend.services.api;
    expect(original).toBeTruthy();
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Delay" }, { timeout: 3000 }));
    await user.type(screen.getByLabelText("Delay in minutes"), "9");
    await user.selectOptions(screen.getByLabelText("Reason code"), "Weather");
    await user.type(screen.getByLabelText(/Note/), "Heavy rain");
    await user.click(screen.getByRole("button", { name: "Set delay" }));

    expect(await screen.findByText("Done. The change is on its way to everyone.")).toBeTruthy();
    const sent = await backend.calls(`POST ${BASE}/delay`)[0]!.json();
    body = sent;
    expect(body).toEqual({ minutes: 9, reasonCode: "Weather", note: "Heavy rain", expectedVersion: 3 });
    expect(screen.getByText("Version 4")).toBeTruthy();
  });

  it("clears a delay the controller set, sending the version in the body of the DELETE, and only offers it for a controller delay", async () => {
    const own = backendFor(journey({ status: { kind: "Late", delayMinutes: 9 } }), "Controller");
    own.routes[`DELETE ${BASE}/delay`] = () => json(200, journey({ version: 4 }));
    renderApp(own.services, `/journeys/${ID}`);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Clear the delay" }, { timeout: 3000 }));

    await screen.findByText("Done. The change is on its way to everyone.");
    expect(await own.calls(`DELETE ${BASE}/delay`)[0]!.json()).toEqual({ expectedVersion: 3 });
  });

  it("does not offer to clear a delay the crew or GPS produced", async () => {
    const backend = backendFor(journey({ status: { kind: "Late", delayMinutes: 9 } }), "Crew");
    renderApp(backend.services, `/journeys/${ID}`);

    await screen.findByRole("button", { name: "Delay" }, { timeout: 3000 });

    expect(screen.queryByRole("button", { name: "Clear the delay" })).toBeNull();
  });

  it("asks for a confirmation that names what a cancellation affects, and needs a reason", async () => {
    const backend = backendFor();
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Cancel journey" }, { timeout: 3000 }));

    const form = screen.getByRole("form", { name: "Cancel the journey" });
    expect(within(form).getByText("This will affect")).toBeTruthy();
    expect(within(form).getByText(/Rambukkana, Kandy \(remaining stops\)/)).toBeTruthy();
    expect(within(form).getByText(/2 stations show "Cancelled"/)).toBeTruthy();
    expect(backend.calls(`POST ${BASE}/cancel`)).toHaveLength(0);   // nothing is sent until the person confirms
  });

  it("shows the server's field errors in words", async () => {
    const backend = backendFor();
    backend.routes[`POST ${BASE}/cancel`] = () => json(400, { code: "VALIDATION_FAILED", title: "The request has errors", status: 400, errors: { reasonCode: ["Give a reason code: SignalFailure."] } });
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Cancel journey" }, { timeout: 3000 }));
    await user.click(within(screen.getByRole("dialog")).getByRole("button", { name: "Cancel journey" }));

    expect((await screen.findAllByText(/Give a reason code/)).length).toBeGreaterThan(0);
  });
});

describe("the platform change dialog (US-O5, the star screen)", () => {
  it("with plenty of notice: shows the normal level, no warning, and what people will see", async () => {
    const backend = backendFor();
    backend.routes[PREVIEW] = () => preview("Normal", 40);
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();

    const form = await openPlatformDialog(user);

    expect(await within(form).findByText(/more than 15 min/)).toBeTruthy();
    expect(within(form).queryByText(/under 5 min/)).toBeNull();
    expect(within(form).getByText("Platform 4 (was 2)")).toBeTruthy();
    expect(within(form).getByText("Now departing from Platform 4")).toBeTruthy();
  });

  it("under 5 minutes: warns before confirming, offers a hold, and promises the train will wait ONLY with the hold", async () => {
    const backend = backendFor();
    backend.routes[PREVIEW] = () => preview("Late", 3);
    backend.routes[PLATFORM] = () => json(200, journey({ version: 4 }));
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();
    const form = await openPlatformDialog(user);

    expect(await within(form).findByText(/under 5 min/)).toBeTruthy();
    expect(within(form).getByLabelText(/Set a hold at the new platform \(recommended\)/)).toBeTruthy();
    expect(within(form).getByText("Now departing from Platform 4")).toBeTruthy();   // no hold: no promise
    expect(within(form).queryByText(/will wait/)).toBeNull();

    await user.click(within(form).getByLabelText(/Set a hold at the new platform/));
    expect(within(form).getByText("The train will wait for passengers at Platform 4")).toBeTruthy();   // only with a hold

    await user.click(within(form).getByRole("button", { name: "Confirm change and hold" }));
    await screen.findByText("Done. The change is on its way to everyone.");
    expect(await backend.calls(PLATFORM)[0]!.json()).toEqual({ platform: "4", setHold: true, holdMinutes: 5, conflictsAcknowledged: false, expectedVersion: 3 });
  });

  it("under 5 minutes without a hold: the change can still be confirmed (a warning, not a block) and says nothing about waiting", async () => {
    const backend = backendFor();
    backend.routes[PREVIEW] = () => preview("Late", 2);
    backend.routes[PLATFORM] = () => json(200, journey({ version: 4 }));
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();
    const form = await openPlatformDialog(user);
    await within(form).findByText(/under 5 min/);

    await user.click(within(form).getByRole("button", { name: "Confirm platform change" }));

    await screen.findByText("Done. The change is on its way to everyone.");
    expect(await backend.calls(PLATFORM)[0]!.json()).toEqual({ platform: "4", setHold: false, holdMinutes: null, conflictsAcknowledged: false, expectedVersion: 3 });
  });

  it("a conflict names the other train and must be acknowledged before Confirm works", async () => {
    const backend = backendFor();
    backend.routes[PREVIEW] = () =>
      preview("ShortNotice", 9, { conflicts: [{ journeyId: "x", serviceNumber: "1520", platform: "4", expectedArrival: "2026-10-14T12:34:00Z", expectedDeparture: null }] });
    backend.routes[PLATFORM] = () => json(200, journey({ version: 4 }));
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();
    const form = await openPlatformDialog(user);

    expect(await within(form).findByText(/Platform 4 is expected to be occupied by 1520 at 18:04/)).toBeTruthy();
    const confirm = within(form).getByRole("button", { name: "Confirm platform change" });
    expect((confirm as HTMLButtonElement).disabled).toBe(true);

    await user.click(within(form).getByLabelText(/I have seen the conflict/));
    expect((confirm as HTMLButtonElement).disabled).toBe(false);
    await user.click(confirm);

    await screen.findByText("Done. The change is on its way to everyone.");
    expect(((await backend.calls(PLATFORM)[0]!.json()) as { conflictsAcknowledged: boolean }).conflictsAcknowledged).toBe(true);
  });

  it("offers only the platforms in service, other than the one the stop already has", async () => {
    const backend = backendFor();
    backend.routes[PREVIEW] = () => preview("Normal", 40);
    renderApp(backend.services, `/journeys/${ID}`);
    const user = userEvent.setup();

    const form = await openPlatformDialog(user);

    const buttons = within(within(form).getByRole("group", { name: "New platform" })).getAllByRole("button") as HTMLButtonElement[];
    const usable = buttons.filter((b) => !b.disabled).map((b) => b.textContent);
    expect(usable).toEqual(["1", "4"]);                                  // not 2 (the current one) and not 5 (out of service)
    expect(buttons.map((b) => b.getAttribute("aria-label"))).toContain("Platform 5, out of service");
  });
});
