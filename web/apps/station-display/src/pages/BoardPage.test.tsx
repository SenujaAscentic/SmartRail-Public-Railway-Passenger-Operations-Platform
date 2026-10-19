import { screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { GAMPAHA, KANDY, POLGAHAWELA, toFort, toKandy } from "../test/data.ts";
import { fakeBackend, renderApp } from "../test/fake-backend.tsx";

// The main departures board (US-D1, US-D4). Time is fake but advances by itself (shouldAdvanceTime), so the page's own
// timers and the testing library's waiting both work; the tests jump ahead where they need minutes.
const NOW = "2026-10-20T01:35:00Z"; // 07:05 in Colombo

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date(NOW));
});

afterEach(() => {
  vi.useRealTimers();
});

const settle = (ms = 0) => vi.advanceTimersByTimeAsync(ms);

async function openBoard(setup?: (b: ReturnType<typeof fakeBackend>) => void, path = "/board/GPH") {
  const fake = fakeBackend();
  setup?.(fake);
  renderApp(fake.services, path);
  await settle(50);
  return fake;
}

describe("the board", () => {
  it("shows the station, the two directions, and each train's time, destination, type, status and platform", async () => {
    await openBoard(({ backend }) => {
      backend.board = [toKandy(1), toFort(1, { time: "2026-10-20T01:38:00Z", expectedTime: "2026-10-20T01:38:00Z" })];
    });

    expect(await screen.findByRole("heading", { name: /Gampaha/ })).toBeTruthy();
    expect(screen.getByText("Departures")).toBeTruthy();
    expect(await screen.findByRole("region", { name: "Towards Kandy" })).toBeTruthy();
    expect(screen.getByRole("region", { name: "Towards Colombo Fort" })).toBeTruthy();

    const toKandyColumn = within(screen.getByRole("region", { name: "Towards Kandy" }));
    expect(toKandyColumn.getByText("07:12")).toBeTruthy(); // 01:42 UTC in Colombo time
    expect(toKandyColumn.getByText("Kandy")).toBeTruthy();
    expect(toKandyColumn.getByText("Express")).toBeTruthy();
    expect(toKandyColumn.getByText("On time")).toBeTruthy();
    expect(toKandyColumn.getByText("1")).toBeTruthy();

    const toFortColumn = within(screen.getByRole("region", { name: "Towards Colombo Fort" }));
    expect(toFortColumn.getByText("07:08")).toBeTruthy();
    expect(toFortColumn.getByText("Colombo Fort")).toBeTruthy();
    expect(toFortColumn.getByText("2")).toBeTruthy();
  });

  it("finds the station by its code without regard to case, and by its id", async () => {
    await openBoard(undefined, "/board/gph");
    expect(await screen.findByRole("heading", { name: /Gampaha/ })).toBeTruthy();

    const byId = await openBoard(undefined, `/board/${KANDY}`);
    expect(byId.calls(/\/lines$/).length).toBeGreaterThan(0);
  });

  it("the clock shows Colombo time", async () => {
    await openBoard();
    expect(await screen.findByText("07:05")).toBeTruthy();
  });

  it("the clock shows the SERVER's time (Date header), not the device's, and keeps ticking from it", async () => {
    // The device says 07:05, the server 3 hours later.
    await openBoard(({ backend }) => (backend.serverShiftMs = 3 * 3600 * 1000));
    expect(await screen.findByText("10:05")).toBeTruthy();
    expect(screen.queryByText("07:05")).toBeNull();

    await settle(62_000); // a minute on (the Date header counts whole seconds): it ticks on from the server time
    expect(screen.getByText("10:06")).toBeTruthy();
  });

  it("asks for the departures of this station, with a limit, without any token, and reads the stream of this station", async () => {
    const fake = await openBoard(({ backend }) => (backend.board = [toKandy(1)]));
    await screen.findByText("Live"); // the board is loaded and the stream is open
    const boardCall = fake.calls(/\/board$/)[0]!;
    const url = new URL(boardCall.url);

    expect(url.pathname).toBe(`/api/v1/stations/${GAMPAHA}/board`);
    expect(url.searchParams.get("kind")).toBe("departures");
    expect(url.searchParams.get("limit")).toBe("30");
    for (const request of fake.backend.requests) expect(request.headers.get("authorization")).toBeNull();
    expect(fake.stream.last().url).toBe(`/api/v1/stream?stationIds=${GAMPAHA}`);
    expect(fake.stream.last().headers.authorization).toBeUndefined();
  });

  it("shows Live when the stream is connected", async () => {
    await openBoard(({ backend }) => (backend.board = [toKandy(1)]));
    expect(await screen.findByText("Live")).toBeTruthy();
  });
});

describe("what a line says", () => {
  it("a late train: the minutes, and the time to expect", async () => {
    await openBoard(({ backend }) => {
      backend.board = [toKandy(1, { status: { kind: "Late", delayMinutes: 8 }, expectedTime: "2026-10-20T01:50:00Z" })];
    });

    expect(await screen.findByText("8 min late")).toBeTruthy();
    expect(screen.getByText("07:20")).toBeTruthy(); // expected 01:50 UTC
    expect(screen.getByText(/Expected/)).toBeTruthy();
  });

  it("an on-time train has no 'Expected' (nothing differs)", async () => {
    await openBoard(({ backend }) => (backend.board = [toKandy(1)]));
    await screen.findByText("On time");
    expect(screen.queryByText(/Expected/)).toBeNull();
  });

  it("a cancelled train stays on the board, says Cancelled and has no platform", async () => {
    await openBoard(({ backend }) => {
      backend.board = [toKandy(1, { status: { kind: "Cancelled", delayMinutes: null }, time: "2026-10-20T01:30:00Z" })];
    });

    expect(await screen.findByText("Cancelled")).toBeTruthy();
    expect(screen.getByLabelText("No platform")).toBeTruthy();
    expect(screen.queryByText("On time")).toBeNull();
  });

  it("a platform change reads 'Platform 5 (was 2)' (S-10): the number and '(was 2)' in the platform cell, the full words as its label", async () => {
    await openBoard(({ backend }) => (backend.board = [toKandy(1, { platform: "5", previousPlatform: "2" })]));

    const cell = await screen.findByLabelText("Platform 5 (was 2)");
    expect(within(cell).getByText("5")).toBeTruthy();
    expect(within(cell).getByText("(was 2)")).toBeTruthy();
  });

  it("a delayed row: 'Expected' sits under the time and the status line holds only the badge (the row keeps its height)", async () => {
    await openBoard(({ backend }) => {
      backend.board = [toKandy(1, { status: { kind: "Late", delayMinutes: 8 }, expectedTime: "2026-10-20T01:50:00Z", platform: "5", previousPlatform: "2" })];
    });

    const expected = (await screen.findByText("Expected")).closest(".line-time-cell")!;
    expect(within(expected as HTMLElement).getByText("07:20")).toBeTruthy(); // the time cell: 07:12 and Expected 07:20
    const line = expected.closest(".line")!.querySelector(".line-status")!;
    expect(within(line as HTMLElement).getByText("8 min late")).toBeTruthy();
    expect(line.querySelectorAll(".status-badge")).toHaveLength(1); // only the status; "(was 2)" is in the platform cell
  });

  it("a train that ends early: 'Terminates at ...' with the termination station", async () => {
    await openBoard(({ backend }) => {
      backend.board = [
        toKandy(1, { status: { kind: "TerminatedEarly", delayMinutes: null }, destination: { stationId: POLGAHAWELA, name: "Polgahawela" } }),
      ];
    });

    expect(await screen.findByText("Terminates at Polgahawela")).toBeTruthy();
  });

  it("a held train: 'Held · 3 min late'", async () => {
    await openBoard(({ backend }) => (backend.board = [toKandy(1, { status: { kind: "Held", delayMinutes: 3 }, hold: true })]));
    expect(await screen.findByText("Held · 3 min late")).toBeTruthy();
  });

  it("a skipped stop says 'Does not stop', an added one 'Extra stop'", async () => {
    await openBoard(({ backend }) => {
      backend.board = [toKandy(1, { stopStatus: "Skipped" }), toKandy(2, { stopStatus: "Added", time: "2026-10-20T01:50:00Z" })];
    });

    expect(await screen.findByText("Does not stop")).toBeTruthy();
    expect(screen.getByText("Extra stop")).toBeTruthy();
  });

  it("a train nobody has started says 'Not started', never 'On time'", async () => {
    await openBoard(({ backend }) => (backend.board = [toKandy(1, { status: { kind: "NotStarted", delayMinutes: null } })]));

    expect(await screen.findByText("Not started")).toBeTruthy();
    expect(screen.queryByText("On time")).toBeNull();
  });

  it("every status has an icon next to its words (never colour alone)", async () => {
    await openBoard(({ backend }) => (backend.board = [toKandy(1), toKandy(2, { status: { kind: "Cancelled", delayMinutes: null }, time: "2026-10-20T01:50:00Z" })]));
    await screen.findByText("Cancelled");

    for (const badge of document.querySelectorAll(".status-badge")) {
      expect(badge.querySelector("svg"), badge.textContent ?? "").not.toBeNull();
      expect(badge.textContent?.trim().length).toBeGreaterThan(0);
    }
  });

  it("trains that only pass the station are not on a departures board", async () => {
    await openBoard(({ backend }) => (backend.board = [toKandy(1, { callType: "Pass", serviceNumber: "9999" }), toKandy(2, { time: "2026-10-20T01:50:00Z" })]));

    await screen.findByText("On time");
    expect(screen.queryByText("9999")).toBeNull();
    expect(screen.getAllByRole("listitem")).toHaveLength(1);
  });

  it("shows at most 10 departures", async () => {
    await openBoard(({ backend }) => {
      backend.board = Array.from({ length: 14 }, (_, i) => toKandy(i + 1, { time: `2026-10-20T0${2 + Math.floor(i / 6)}:${String((i % 6) * 10).padStart(2, "0")}:00Z` }));
    });

    await screen.findByText("Departures");
    expect(screen.getAllByRole("listitem")).toHaveLength(10);
  });
});

describe("states", () => {
  it("says it is loading until the first answer", async () => {
    const fake = fakeBackend();
    fake.backend.hangBoard = true;
    renderApp(fake.services, "/board/GPH");
    await settle(50);

    expect(await screen.findByText("Loading")).toBeTruthy();
    expect(screen.queryByText("No more departures today")).toBeNull();
  });

  it("an unknown station says so", async () => {
    await openBoard(undefined, "/board/XYZ");
    expect(await screen.findByText("This station is not known.")).toBeTruthy();
  });

  it("when the lines cannot be loaded it says so and tries again by itself", async () => {
    const fake = fakeBackend();
    fake.backend.failLines = true;
    renderApp(fake.services, "/board/GPH");
    await settle(50);
    expect(await screen.findByText("The timetable could not be loaded. Please ask staff.")).toBeTruthy();

    fake.backend.failLines = false;
    fake.backend.board = [toKandy(1)];
    await settle(5500);

    expect(await screen.findByRole("heading", { name: /Gampaha/ })).toBeTruthy();
  });

  it("no departures left: 'No more departures today'", async () => {
    await openBoard(({ backend }) => (backend.board = []));
    expect(await screen.findByText("No more departures today")).toBeTruthy();
  });

  it("an address that is not a screen says so", async () => {
    await openBoard(undefined, "/somewhere");
    expect(await screen.findByText(/This page does not exist/)).toBeTruthy();
  });
});

describe("live updates", () => {
  it("an event makes the board read again: the new delay appears without a reload", async () => {
    const fake = await openBoard(({ backend }) => (backend.board = [toKandy(1)]));
    expect(await screen.findByText("On time")).toBeTruthy();

    fake.backend.board = [toKandy(1, { status: { kind: "Late", delayMinutes: 6 }, expectedTime: "2026-10-20T01:48:00Z" })];
    fake.stream.event("DelayChanged", { sequence: 7 }, 7);
    await settle(600);

    expect(await screen.findByText("6 min late")).toBeTruthy();
    expect(screen.queryByText("On time")).toBeNull();
  });

  it("a new train appears and a departed one disappears when the board changes", async () => {
    const fake = await openBoard(({ backend }) => (backend.board = [toKandy(1), toKandy(2, { time: "2026-10-20T01:50:00Z", expectedTime: "2026-10-20T01:50:00Z" })]));
    await screen.findByText("07:12");

    fake.backend.board = [toKandy(2, { time: "2026-10-20T01:50:00Z", expectedTime: "2026-10-20T01:50:00Z" })];
    fake.stream.event("StopStatusChanged", {}, 3);
    await settle(600);

    expect(screen.queryByText("07:12")).toBeNull();
    expect(screen.getByText("07:20")).toBeTruthy();
  });

  it("a heartbeat does not read the board again", async () => {
    const fake = await openBoard(({ backend }) => (backend.board = [toKandy(1)]));
    await screen.findByText("On time");
    const before = fake.calls(/\/board$/).length;

    fake.stream.heartbeat();
    await settle(2000);

    expect(fake.calls(/\/board$/).length).toBe(before);
  });
});

describe("offline (US-D4)", () => {
  it("after 2 minutes without any contact: the banner, 'Offline', and 'Scheduled' instead of any status", async () => {
    const fake = await openBoard(({ backend }) => {
      backend.board = [toKandy(1, { time: "2026-10-20T01:50:00Z", status: { kind: "Late", delayMinutes: 5 }, expectedTime: "2026-10-20T01:55:00Z" })];
    });
    expect(await screen.findByText("5 min late")).toBeTruthy();

    fake.backend.down = true;
    fake.stream.state.refuse = true;
    fake.stream.close();
    await settle(119_000);
    expect(screen.queryByText("Live information unavailable – check announcements or ask staff")).toBeNull();
    await settle(3_000);

    expect(screen.getByText("Live information unavailable – check announcements or ask staff")).toBeTruthy();
    expect(screen.getByText("Offline")).toBeTruthy();
    expect(screen.getAllByText("Scheduled").length).toBeGreaterThan(0);
    expect(screen.queryByText("5 min late")).toBeNull();
    expect(screen.queryByText(/Expected/)).toBeNull();
    // The time that stays is the scheduled one.
    expect(screen.getByText("07:20")).toBeTruthy();
  });

  it("never says 'On time' while offline", async () => {
    const fake = await openBoard(({ backend }) => (backend.board = [toKandy(1, { time: "2026-10-20T01:50:00Z" })]));
    expect(await screen.findByText("On time")).toBeTruthy();

    fake.backend.down = true;
    fake.stream.state.refuse = true;
    fake.stream.close();
    await settle(125_000);

    expect(screen.getByText("Offline")).toBeTruthy();
    expect(screen.queryByText("On time")).toBeNull();
  });

  it("offline, trains are removed as their scheduled time passes", async () => {
    const fake = await openBoard(({ backend }) => {
      backend.board = [toKandy(1, { time: "2026-10-20T01:36:00Z" }), toKandy(2, { time: "2026-10-20T02:30:00Z" })];
    });
    await screen.findByText("07:06");

    fake.backend.down = true;
    fake.stream.state.refuse = true;
    fake.stream.close();
    await settle(125_000);

    expect(screen.getByText("Offline")).toBeTruthy();
    expect(screen.queryByText("07:06")).toBeNull();
    expect(screen.getByText("08:00")).toBeTruthy();
  });

  it("offline, trains are removed by the SERVER's time, not the device's", async () => {
    // The server is an hour behind the device: 06:05 when the device says 07:05.
    const fake = await openBoard(({ backend }) => {
      backend.serverShiftMs = -3600 * 1000;
      backend.board = [toKandy(1, { time: "2026-10-20T01:36:00Z" })]; // 07:06 by the device
    });
    await screen.findByText("07:06");

    fake.backend.down = true;
    fake.stream.state.refuse = true;
    fake.stream.close();
    await settle(125_000); // device 07:07, server 06:07: the train is still to come

    expect(screen.getByText("Offline")).toBeTruthy();
    expect(screen.getByText("07:06")).toBeTruthy();
  });

  it("a cancelled train that is offline is shown as scheduled, not as cancelled (nothing live is known)", async () => {
    const fake = await openBoard(({ backend }) => {
      backend.board = [toKandy(1, { time: "2026-10-20T03:30:00Z", status: { kind: "Cancelled", delayMinutes: null } })];
    });
    await screen.findByText("Cancelled");

    fake.backend.down = true;
    fake.stream.state.refuse = true;
    fake.stream.close();
    await settle(125_000);

    expect(screen.queryByText("Cancelled")).toBeNull();
    expect(screen.getAllByText("Scheduled").length).toBeGreaterThan(0);
  });

  it("is back to live when the connection returns: the banner goes and the board is read in full", async () => {
    const fake = await openBoard(({ backend }) => (backend.board = [toKandy(1, { time: "2026-10-20T01:50:00Z" })]));
    await screen.findByText("On time");

    fake.backend.down = true;
    fake.stream.state.refuse = true;
    fake.stream.close();
    await settle(125_000);
    expect(screen.getByText("Offline")).toBeTruthy();

    // Meanwhile a delay happened. The connection returns.
    fake.backend.board = [toKandy(1, { time: "2026-10-20T01:50:00Z", status: { kind: "Late", delayMinutes: 9 }, expectedTime: "2026-10-20T01:59:00Z" })];
    fake.backend.down = false;
    fake.stream.state.refuse = false;
    await settle(35_000); // the next poll, or the next reconnect

    expect(screen.queryByText("Live information unavailable – check announcements or ask staff")).toBeNull();
    expect(await screen.findByText("9 min late")).toBeTruthy();
    expect(screen.getByText("Live")).toBeTruthy();
  });

  it("is not offline while only the stream is down but the board still answers", async () => {
    const fake = await openBoard(({ backend }) => (backend.board = [toKandy(1, { time: "2026-10-20T03:30:00Z" })]));
    await screen.findByText("On time");

    fake.stream.state.refuse = true;
    fake.stream.close();
    await settle(5 * 60_000);

    expect(screen.queryByText("Offline")).toBeNull();
    expect(screen.queryByText("Live information unavailable – check announcements or ask staff")).toBeNull();
  });
});
