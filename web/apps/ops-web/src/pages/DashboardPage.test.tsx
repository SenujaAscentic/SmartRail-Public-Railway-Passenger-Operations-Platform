import { screen, within } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { REFRESH, fakeBackend, renderApp } from "../test/fake-server.tsx";
import { journey, snapshot, KANDY } from "../test/fixtures.ts";
import { frame, sse } from "../test/sse.ts";

const LATE = "11111111-1111-4111-8111-111111111111";
const FINE = "22222222-2222-4222-8222-222222222222";
const CANCELLED = "33333333-3333-4333-8333-333333333333";
const LOST = "44444444-4444-4444-8444-444444444444";

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

    const table = await screen.findByRole("table", {}, { timeout: 3000 });
    const rows = within(table).getAllByRole("row").slice(1);
    expect(rows).toHaveLength(2);                                   // 2001 is on time: not here
    expect(within(rows[0]!).getByText(/1002/)).toBeTruthy();       // cancelled counts as the worst
    expect(within(rows[0]!).getByText("Cancelled")).toBeTruthy();
    expect(within(rows[1]!).getByText("8 min late")).toBeTruthy();
  });

  it("shows where the delay came from", async () => {
    const backend = signedInWith(all);

    renderApp(backend.services, "/");

    const row = (await screen.findByRole("row", { name: /1001/ }, { timeout: 3000 }));
    expect(within(row).getByText(/Driver/)).toBeTruthy();   // a crew value
  });

  it("raises an alert for a running train whose position is lost, in words as well as colour", async () => {
    const backend = signedInWith(all);

    renderApp(backend.services, "/");

    const alerts = (await screen.findByRole("heading", { name: "Alerts" }, { timeout: 3000 })).closest("section")!;
    expect(await within(alerts).findByText("Live location unavailable – scheduled times shown")).toBeTruthy();
    expect(within(alerts).getByRole("link", { name: /1003/ })).toBeTruthy();
  });

  it("puts a running train on the line, between its last and next station, with the backend's fraction", async () => {
    const backend = signedInWith(all);

    renderApp(backend.services, "/");

    const line = (await screen.findByRole("heading", { name: "Line view" }, { timeout: 3000 })).closest("section")!;
    // Two trains are running with a position (1001 and 2001); the cancelled one and the one without a position are not on the line.
    expect(await within(line).findAllByText(/25% of the way to Rambukkana/)).toHaveLength(2);
    expect(within(line).getByText("Kandy")).toBeTruthy();
  });

  it("filters by section: a journey that does not run between the two chosen stations is hidden", async () => {
    const backend = signedInWith(all);
    renderApp(backend.services, "/");
    await screen.findByRole("row", { name: /1001/ }, { timeout: 3000 });

    // Kandy to Fort is the opposite direction of these trains.
    const user = (await import("@testing-library/user-event")).default.setup();
    await user.selectOptions(screen.getByLabelText("Section from"), KANDY);
    await user.selectOptions(screen.getByLabelText("Section to"), "3f1b6a52-8c0d-4e7a-b1f4-2d9c5e7a0b13");

    expect(screen.queryByRole("row", { name: /1001/ })).toBeNull();
    expect(screen.getAllByText("No disrupted trains.")).toHaveLength(1);
  });

  it("says what the connection is doing", async () => {
    const backend = signedInWith(all);

    renderApp(backend.services, "/");

    expect(await screen.findByText("Live", {}, { timeout: 3000 })).toBeTruthy();
  });
});
