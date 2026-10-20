import { screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { REFRESH, fakeBackend, renderApp } from "../test/fake-server.tsx";
import { journey, snapshot, KANDY } from "../test/fixtures.ts";
import { frame, sse } from "../test/sse.ts";

const LATE = "11111111-1111-4111-8111-111111111111";
const FINE = "22222222-2222-4222-8222-222222222222";
const CANCELLED = "33333333-3333-4333-8333-333333333333";
const LOST = "44444444-4444-4444-8444-444444444444";

// The "Disrupted trains" panel: the page also lists every train of the day, so a row query must say which table it means.
const disruptedPanel = async () => (await screen.findByRole("heading", { name: "Disrupted trains" }, { timeout: 3000 })).closest("section")!;

function signedInWith(frames: string) {
  const backend = fakeBackend();
  backend.routes[REFRESH] = () => backend.tokenResponse("t");
  backend.stream.responder = sse(frames, { hold: true });
  return backend;
}

const all =
  frame("snapshot", snapshot(journey({ id: LATE, serviceNumber: "1001", status: { kind: "Late", delayMinutes: 8 } }), "Crew"), 10) +
  frame("snapshot", snapshot(journey({ id: FINE, serviceNumber: "2001", name: "Kandy Slow" }), "Gps"), 10) +
  frame("snapshot", snapshot(journey({ id: CANCELLED, serviceNumber: "1002", status: { kind: "Cancelled", delayMinutes: null } }), null), 10) +
  frame(
    "snapshot",
    snapshot(journey({ id: LOST, serviceNumber: "1003", freshness: { tracking: "Lost", updatedAt: "2026-10-14T01:00:00Z", lastStationName: "Colombo Fort", lastPosition: null } }), "Crew"),
    10,
  );

describe("the dashboard", () => {
  it("lists the disrupted trains worst first and leaves the on-time ones out", async () => {
    const backend = signedInWith(all);

    renderApp(backend.services, "/");

    const rows = (await within(await disruptedPanel()).findAllByRole("row", {}, { timeout: 3000 })).slice(1);
    expect(rows).toHaveLength(2);                                   // 2001 is on time: not here
    expect(within(rows[0]!).getByRole("rowheader", { name: /1002/ })).toBeTruthy();   // cancelled counts as the worst
    expect(within(rows[0]!).getByText("Cancelled")).toBeTruthy();
    expect(within(rows[1]!).getByText("8 min late")).toBeTruthy();
  });

  it("lists every train of the day in the order they leave, started or not, each with a link to its journey (so a train can be cancelled before it starts)", async () => {
    const notStarted = journey({
      id: "55555555-5555-4555-8555-555555555555", serviceNumber: "1002", status: { kind: "NotStarted", delayMinutes: null },
      from: { ...journey().from, scheduledDeparture: "2026-10-14T05:00:00Z", expectedDeparture: "2026-10-14T05:00:00Z" },
    });
    const returning = journey({
      id: "66666666-6666-4666-8666-666666666666", serviceNumber: "1004", name: "Colombo Fort Express",
      from: { ...journey().from, stationId: KANDY, name: "Kandy", scheduledDeparture: "2026-10-14T03:00:00Z", expectedDeparture: "2026-10-14T03:00:00Z" },
      to: { ...journey().to, stationId: journey().from.stationId, name: "Colombo Fort" },
    });
    const backend = signedInWith(
      frame("snapshot", snapshot(notStarted, null), 10) + frame("snapshot", snapshot(journey({ id: FINE, serviceNumber: "2001" }), "Gps"), 10) + frame("snapshot", snapshot(returning, "Gps"), 10),
    );

    renderApp(backend.services, "/");

    const section = (await screen.findByRole("heading", { name: "Today's trains" }, { timeout: 3000 })).closest("section")!;
    const rows = await within(section).findAllByRole("row");
    expect(rows).toHaveLength(4);   // the header and three trains
    expect(within(rows[1]!).getByRole("rowheader", { name: /2001/ })).toBeTruthy();   // Fort 06:30 Colombo time
    expect(within(rows[2]!).getByRole("rowheader", { name: /1004/ })).toBeTruthy();   // Kandy 08:30, the other way
    expect(within(rows[3]!).getByRole("rowheader", { name: /1002/ })).toBeTruthy();   // Fort 10:30
    expect(within(rows[3]!).getByText("Not started")).toBeTruthy();
    expect(within(rows[3]!).getByRole("link", { name: /Open 1002/ }).getAttribute("href")).toBe("/journeys/55555555-5555-4555-8555-555555555555");
  });

  it("shows where the delay came from", async () => {
    const backend = signedInWith(all);

    renderApp(backend.services, "/");

    const row = await within(await disruptedPanel()).findByRole("row", { name: /1001/ }, { timeout: 3000 });
    expect(within(row).getByText(/Driver/)).toBeTruthy();   // a crew value
  });

  it("raises an alert for a running train whose position is lost, in words as well as colour", async () => {
    const backend = signedInWith(all);

    renderApp(backend.services, "/");

    const alerts = (await screen.findByRole("heading", { name: "Alerts" }, { timeout: 3000 })).closest("section")!;
    expect(await within(alerts).findByText(/Live location unavailable – scheduled times shown/)).toBeTruthy();
    expect(within(alerts).getByText(/1003/)).toBeTruthy();
    expect(within(alerts).getByRole("link", { name: "Open journey" })).toBeTruthy();
  });

  it("puts a running train on the line, between its last and next station, with the backend's fraction", async () => {
    const backend = signedInWith(all);

    renderApp(backend.services, "/");

    const line = (await screen.findByRole("heading", { name: "Line view" }, { timeout: 3000 })).closest("section")!;
    // Two trains are running with a position (1001 and 2001); the cancelled one and the one without a position are not on the line.
    expect(await within(line).findAllByText(/between Colombo Fort and Rambukkana/)).toHaveLength(2);
    expect(within(line).getByText("Kandy")).toBeTruthy();
  });

  it("draws a pin ON the line for each running train, at the backend's place, with an icon, the service number and the status colour", async () => {
    const backend = signedInWith(all);

    renderApp(backend.services, "/");

    const line = (await screen.findByRole("heading", { name: "Line view" }, { timeout: 3000 })).closest("section")!;
    await within(line).findAllByText(/between Colombo Fort and Rambukkana/);
    const pins = Array.from(line.querySelectorAll<HTMLElement>(".track-pin"));
    expect(pins).toHaveLength(2);   // 1001 and 2001 are running with a position
    const late = pins.find((pin) => pin.textContent === "1001")!;
    expect(late.className).toContain("status-late");                // coloured by status ...
    expect(late.querySelector("svg")).toBeTruthy();                  // ... with an icon ...
    expect(late.textContent).toBe("1001");                           // ... and the service number
    expect(late.style.left).toBe("25%");                             // 25% of the way from Fort to Rambukkana: (0.25 + 0.5) / 3 stations
    expect(late.closest("[aria-hidden='true']")).toBeTruthy();      // the card keeps the details and the link
  });

  it("filters by section: a journey that does not run between the two chosen stations is hidden", async () => {
    const backend = signedInWith(all);
    renderApp(backend.services, "/");
    await within(await disruptedPanel()).findByRole("row", { name: /1001/ }, { timeout: 3000 });

    // Kandy to Fort is the opposite direction of these trains.
    const user = (await import("@testing-library/user-event")).default.setup();
    await user.selectOptions(screen.getByLabelText("Section from"), KANDY);
    await user.selectOptions(screen.getByLabelText("Section to"), "3f1b6a52-8c0d-4e7a-b1f4-2d9c5e7a0b13");

    expect(within(await disruptedPanel()).queryByRole("row", { name: /1001/ })).toBeNull();
    expect(within((await screen.findByRole("heading", { name: "Today's trains" })).closest("section")!).queryByRole("row", { name: /1001/ })).toBeNull();
    expect(screen.getAllByText("No disrupted trains.")).toHaveLength(1);
  });

  it("says what the connection is doing", async () => {
    const backend = signedInWith(all);

    renderApp(backend.services, "/");

    expect(await screen.findByText("Live · connected", {}, { timeout: 3000 })).toBeTruthy();
  });
});
