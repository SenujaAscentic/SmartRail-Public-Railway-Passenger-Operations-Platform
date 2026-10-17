import { screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { frame, sse } from "../test/sse.ts";
import { REFRESH, fakeBackend, renderApp } from "../test/fake-server.tsx";

// The bare live page: a connection to GET /api/v1/stream for the Main Line, and a list of what arrives.
const LINE = "5d1c8e0a-2b47-4f6a-9c33-71a0e4d8b2f6";

const snapshot = { journeyId: "0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01", serviceDate: "2026-10-14", lineId: LINE, delayMinutes: 4, tracking: "Live", stops: [], sequence: 1051 };
const delayChanged = { eventId: "2c9f5b81", sequence: 1043, type: "DelayChanged", version: 1, occurredAt: "2026-10-14T06:44:00Z", journeyId: "0199f0a2-7c3e-7b10-8a4d-3f6e2c1b9a01", stationIds: [], lineId: LINE, payload: { delayMinutes: 4 } };

afterEach(() => vi.unstubAllEnvs());

// A signed-in person (the refresh cookie is good) looking at /live, with the Main Line configured.
function openLive(text: string, options: { hold?: boolean; line?: string } = {}) {
  vi.stubEnv("VITE_MAIN_LINE_ID", options.line ?? LINE);
  const backend = fakeBackend();
  backend.routes[REFRESH] = () => backend.tokenResponse("token-1");
  backend.stream.responder = sse(text, { hold: options.hold ?? true });
  return { ...backend, ...renderApp(backend.services, "/live") };
}

describe("the stream request", () => {
  it("asks for the Main Line's events with the token in a header", async () => {
    const { stream } = openLive("");
    await waitFor(() => expect(stream.requests).toHaveLength(1));

    const url = new URL(stream.requests[0]!.url, "http://localhost");
    expect(url.pathname).toBe("/api/v1/stream");
    expect(url.searchParams.get("lineIds")).toBe(LINE);
    expect(stream.requests[0]!.headers.authorization).toBe("Bearer token-1");
    expect(stream.requests[0]!.url).not.toContain("token-1");
  });

  it("stops the connection when the person leaves the page", async () => {
    const { stream, unmount } = openLive("");
    await waitFor(() => expect(stream.requests).toHaveLength(1));

    unmount();

    await waitFor(() => expect(stream.signals[0]?.aborted).toBe(true));
  });
});

describe("what the page shows", () => {
  it("says 'Live' once connected, in words and an icon (never colour alone)", async () => {
    openLive("");

    const badge = await screen.findByText("Live");
    expect(badge.closest("[role='status']")?.querySelector("svg[aria-hidden='true']")).toBeTruthy();
  });

  it("lists the events newest first, with the sequence, the Colombo time and the journey", async () => {
    openLive(frame("snapshot", snapshot, 1051) + frame("DelayChanged", delayChanged, 1052));

    const table = await screen.findByRole("table", { name: /most recent events/i });
    await waitFor(() => expect(table.querySelectorAll("tbody tr")).toHaveLength(2));
    const rows = Array.from(table.querySelectorAll("tbody tr")).map((row) => row.textContent);
    expect(rows[0]).toContain("DelayChanged");
    expect(rows[0]).toContain("1052");
    expect(rows[1]).toContain("snapshot");
    expect(rows[1]).toContain("0199f0a2");   // the first part of the journey id
    // A frame without occurredAt shows a dash; an envelope shows Sri Lanka time.
    expect(rows[1]).toContain("—");
  });

  it("shows the time of an event in Asia/Colombo", async () => {
    openLive(frame("DelayChanged", delayChanged, 1043));

    expect(await screen.findByText("14 Oct, 12:14:00")).toBeTruthy();
  });

  it("counts what it has received", async () => {
    openLive(frame("snapshot", snapshot) + frame("snapshot", { ...snapshot, journeyId: "b" }) + frame("DelayChanged", delayChanged, 1043));

    expect(await screen.findByText(/Received 3 messages \(2 snapshots\)/)).toBeTruthy();
  });

  it("does not list the heartbeat as an event", async () => {
    openLive(": keep-alive\n\n" + frame("DelayChanged", delayChanged, 1043));

    await screen.findByText("DelayChanged");
    expect(screen.getAllByRole("row")).toHaveLength(2);   // the header row and the one event
  });

  it("on a resync drops the old events and says that the view was reloaded", async () => {
    openLive(frame("DelayChanged", delayChanged, 1043) + frame("resync", { reason: "The missed events are no longer kept." }) + frame("snapshot", snapshot, 1100));

    expect(await screen.findByText(/view was reloaded from a fresh snapshot/)).toBeTruthy();
    await waitFor(() => expect(screen.queryByText("DelayChanged")).toBeNull());
    expect(screen.getByText("snapshot")).toBeTruthy();
  });

  it("shows the data of the latest event as plain text", async () => {
    openLive(frame("DelayChanged", { ...delayChanged, payload: { note: "<b>not html</b>" } }, 1043));

    const pre = await screen.findByText(/not html/);
    expect(pre.tagName).toBe("PRE");
    expect(document.querySelector("pre b")).toBeNull();   // announcements and notes are plain text, never HTML (CLAUDE.md section 6)
  });
});

describe("when the connection does not work", () => {
  it("says 'Not connected' and why when the server refuses the request", async () => {
    vi.stubEnv("VITE_MAIN_LINE_ID", LINE);
    const backend = fakeBackend();
    backend.routes[REFRESH] = () => backend.tokenResponse("t");
    backend.stream.responder = () => new Response(JSON.stringify({ code: "VALIDATION_FAILED", status: 400 }), { status: 400, headers: { "content-type": "application/problem+json" } });
    renderApp(backend.services, "/live");

    expect(await screen.findByText("Not connected")).toBeTruthy();
    expect(screen.getByRole("alert").textContent).toContain("(400)");
  });

  it("says 'Reconnecting…' while it tries again", async () => {
    vi.stubEnv("VITE_MAIN_LINE_ID", LINE);
    const backend = fakeBackend();
    backend.routes[REFRESH] = () => backend.tokenResponse("t");
    backend.stream.responder = () => new Response(null, { status: 503 });
    renderApp(backend.services, "/live");

    expect(await screen.findByText("Reconnecting…")).toBeTruthy();
  });
});

describe("the Main Line setting", () => {
  it("explains how to set it when it is missing, and does not connect", async () => {
    vi.stubEnv("VITE_MAIN_LINE_ID", "");
    const backend = fakeBackend();
    backend.routes[REFRESH] = () => backend.tokenResponse("t");
    renderApp(backend.services, "/live");

    expect((await screen.findByRole("alert")).textContent).toContain("VITE_MAIN_LINE_ID");
    expect(backend.stream.requests).toHaveLength(0);
  });

  it("refuses a value that is not a GUID, and does not connect", async () => {
    const { stream } = openLive("", { line: "main-line" });

    expect((await screen.findByRole("alert")).textContent).toContain("not a valid id");
    expect(stream.requests).toHaveLength(0);
  });
});

describe("who may see it", () => {
  it("sends a person who is not signed in to the sign-in page, without connecting", async () => {
    vi.stubEnv("VITE_MAIN_LINE_ID", LINE);
    const backend = fakeBackend();   // the refresh is refused: nobody is signed in
    renderApp(backend.services, "/live");

    expect(await screen.findByRole("heading", { level: 1, name: "Sign in" })).toBeTruthy();
    expect(backend.stream.requests).toHaveLength(0);
  });
});
