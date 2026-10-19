import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { describe, expect, it } from "vitest";
import { ME, REFRESH, fakeBackend, json, renderApp } from "../test/fake-server.tsx";

const AUDIT = "GET /api/v1/ops/audit";

const entry = (action: string, summary: string) => ({
  id: crypto.randomUUID(),
  at: "2026-10-14T04:30:00Z",
  actor: { id: crypto.randomUUID(), staffNumber: "CTRL-01", name: "Demo Controller", role: "Controller" },
  action,
  targetType: "Journey",
  targetId: "0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01",
  reason: action === "DelaySet" ? "Weather" : null,
  summary,
  requestId: "r",
});

function backend() {
  const b = fakeBackend();
  b.routes[REFRESH] = () => b.tokenResponse("t");
  b.routes[AUDIT] = () => json(200, [entry("DelayCleared", "Controller delay cleared"), entry("DelaySet", "Delay set to 6 min")]);
  return b;
}

describe("the audit log", () => {
  it("shows who did what and when, in Sri Lanka time, once asked", async () => {
    const b = backend();
    renderApp(b.services, "/audit");
    const user = userEvent.setup();

    await user.click(await screen.findByRole("button", { name: "Apply" }, { timeout: 3000 }));

    const table = await screen.findByRole("table");
    const rows = within(table).getAllByRole("row").slice(1);
    expect(rows).toHaveLength(2);
    expect(within(rows[0]!).getByText(/DelayCleared/)).toBeTruthy();
    expect(within(rows[1]!).getByText("Demo Controller (CTRL-01)")).toBeTruthy();
    expect(within(rows[1]!).getByText("14 Oct, 10:00:00")).toBeTruthy();   // 04:30 UTC
    expect(within(rows[1]!).getByText("Weather")).toBeTruthy();
  });

  it("sends the filters it was given, with the times as UTC", async () => {
    const b = backend();
    renderApp(b.services, "/audit");
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Staff number", {}, { timeout: 3000 }), "CTRL-01");
    await user.type(screen.getByLabelText("Journey id"), "0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01");
    await user.type(screen.getByLabelText("From (Sri Lanka time)"), "2026-10-14T10:00");
    await user.click(screen.getByRole("button", { name: "Apply" }));
    await screen.findByRole("table");

    const url = new URL(b.calls(AUDIT)[0]!.url);
    expect(url.searchParams.get("staff")).toBe("CTRL-01");
    expect(url.searchParams.get("journeyId")).toBe("0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01");
    expect(url.searchParams.get("from")).toBe("2026-10-14T04:30:00.000Z");
  });

  it("shows the server's complaint about a filter next to the field", async () => {
    const b = backend();
    b.routes[AUDIT] = () => json(400, { code: "VALIDATION_FAILED", title: "The request has errors", status: 400, errors: { journeyId: ["The journey id is not valid."] } });
    renderApp(b.services, "/audit");
    const user = userEvent.setup();

    await user.type(await screen.findByLabelText("Journey id", {}, { timeout: 3000 }), "nope");
    await user.click(screen.getByRole("button", { name: "Apply" }));

    expect(await screen.findByText("The journey id is not valid.")).toBeTruthy();
  });

  it("is for the controller only", async () => {
    const b = backend();
    b.routes[ME] = () => json(200, { name: "Demo Manager", role: "StationManager", stationId: "3f1b6a52-8c0d-4e7a-b1f4-2d9c5e7a0b13" });
    renderApp(b.services, "/audit");

    expect(await screen.findByText("Only the controller can read the audit log.", {}, { timeout: 3000 })).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Apply" })).toBeNull();
  });
});
