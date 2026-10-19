import { screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { GAMPAHA, journey, KANDY, toFort, toKandy, VEYANGODA } from "../test/data.ts";
import { fakeBackend, renderApp } from "../test/fake-backend.tsx";

// The platform display (US-D2, US-D4): /platform/GPH/1 is Platform 1 of Gampaha.
const NOW = "2026-10-20T01:35:00Z"; // 07:05 in Colombo

beforeEach(() => {
  vi.useFakeTimers({ shouldAdvanceTime: true });
  vi.setSystemTime(new Date(NOW));
});

afterEach(() => {
  vi.useRealTimers();
});

const settle = (ms = 0) => vi.advanceTimersByTimeAsync(ms);

// A board for platform 1 at 07:12, 07:30 and 07:45, the first on time, the second late.
const next = () => toKandy(1);
const second = () => toKandy(2, { trainType: "Slow", time: "2026-10-20T02:00:00Z", expectedTime: "2026-10-20T02:00:00Z" });
const third = () => toKandy(3, { trainType: "Slow", time: "2026-10-20T02:15:00Z", expectedTime: "2026-10-20T02:23:00Z", status: { kind: "Late", delayMinutes: 8 } });

async function openPlatform(setup?: (f: ReturnType<typeof fakeBackend>) => void, path = "/platform/GPH/1") {
  const fake = fakeBackend();
  fake.backend.board = [next(), second(), third()];
  fake.withJourney(next());
  setup?.(fake);
  renderApp(fake.services, path);
  await settle(50);
  return fake;
}

describe("the next train", () => {
  it("shows the station and direction, the platform, the next train with its time, type, status and expected time, and the following two", async () => {
    await openPlatform();

    expect(await screen.findByRole("heading", { name: /Gampaha/ })).toBeTruthy();
    expect(await screen.findByText("Towards Kandy")).toBeTruthy();
    expect(screen.getByLabelText("Platform 1")).toBeTruthy();

    const nextPanel = within(await screen.findByRole("region", { name: "Next" }));
    // the scheduled time and the expected time (both 07:12 for an on-time train)
    expect(nextPanel.getAllByText("07:12")).toHaveLength(2);
    expect(nextPanel.getByText("Kandy")).toBeTruthy();
    expect(nextPanel.getByText("Express")).toBeTruthy();
    expect(nextPanel.getByText("On time")).toBeTruthy();
    expect(nextPanel.getByText("Expected")).toBeTruthy();

    const then = within(screen.getByRole("region", { name: "Then" }));
    expect(then.getAllByRole("listitem")).toHaveLength(2);
    expect(then.getAllByText("07:30")).toHaveLength(2); // scheduled and expected
    expect(then.getByText("8 min late")).toBeTruthy();
    expect(then.getByText("07:53")).toBeTruthy(); // expected 02:23 UTC
  });

  it("'Calling at' lists the stations after this one where the train stops (not the ones it passes)", async () => {
    await openPlatform();

    const calling = await screen.findByText("Polgahawela · Kandy");
    expect(calling).toBeTruthy();
    expect(screen.queryByText(/Veyangoda/)).toBeNull();
    expect(screen.getByText("Calling at")).toBeTruthy();
  });

  it("asks for the journey of the next train, once", async () => {
    const fake = await openPlatform();
    await screen.findByText("Polgahawela · Kandy");

    const calls = fake.calls(/\/journeys\//);
    expect(calls.length).toBeGreaterThan(0);
    for (const call of calls) expect(new URL(call.url).pathname).toBe(`/api/v1/journeys/${next().journeyId}`);
  });

  it("only shows trains of this platform", async () => {
    await openPlatform(({ backend }) => {
      backend.board = [toFort(1), next(), toFort(2, { time: "2026-10-20T02:05:00Z" })];
    });

    const nextPanel = within(await screen.findByRole("region", { name: "Next" }));
    expect(nextPanel.getByText("Kandy")).toBeTruthy();
    expect(screen.queryByRole("region", { name: "Then" })).toBeNull();
  });

  it("a long list of stations scrolls", async () => {
    const stops = journey({ journeyId: next().journeyId });
    for (let i = 0; i < 12; i++) {
      stops.stops.push({ ...stops.stops[5]!, sequence: 10 + i, stationId: `x-${i}`, name: `Station number ${i}`, status: "Scheduled" });
    }
    await openPlatform(({ backend }) => backend.journeys.set(next().journeyId, stops));

    expect((await screen.findAllByText(/Station number 11/)).length).toBeGreaterThan(0);
    expect(document.querySelector(".calling-list.scrolling")).not.toBeNull();
  });

  it("a short list does not scroll", async () => {
    await openPlatform();
    await screen.findByText("Polgahawela · Kandy");
    expect(document.querySelector(".calling-list.scrolling")).toBeNull();
  });

  it("without the journey (it cannot be read) the rest still works, just without 'Calling at'", async () => {
    await openPlatform(({ backend }) => backend.journeys.clear());

    expect(await screen.findByRole("region", { name: "Next" })).toBeTruthy();
    expect(screen.queryByText("Calling at")).toBeNull();
  });

  it("a delayed next train shows its minutes and the time to expect", async () => {
    await openPlatform(({ backend }) => {
      backend.board = [toKandy(1, { status: { kind: "Late", delayMinutes: 5 }, expectedTime: "2026-10-20T01:47:00Z" })];
    });

    const nextPanel = within(await screen.findByRole("region", { name: "Next" }));
    expect(nextPanel.getByText("5 min late")).toBeTruthy();
    expect(nextPanel.getByText("07:17")).toBeTruthy();
  });

  it("an empty platform says so", async () => {
    await openPlatform(({ backend }) => (backend.board = [toFort(1)]));
    expect(await screen.findByText("No trains on this platform")).toBeTruthy();
  });
});

describe("a train that does not stop here (S-18)", () => {
  it("shows the warning with its icon and the exact words, and no stations to call at", async () => {
    await openPlatform(({ backend }) => {
      backend.board = [toKandy(1, { callType: "Pass" }), second()];
    });

    const warning = await screen.findByText("Next train does not stop here – stand back from the platform edge");
    expect(warning.closest(".notice-warning")!.querySelector("svg")).not.toBeNull();
    expect(screen.queryByText("Calling at")).toBeNull();
    expect(within(screen.getByRole("region", { name: "Next" })).getByText("Does not stop")).toBeTruthy();
  });

  it("is not shown when the next train stops", async () => {
    await openPlatform();
    await screen.findByRole("region", { name: "Next" });
    expect(screen.queryByText(/does not stop here/)).toBeNull();
  });

  it("the following trains are still listed after a passing one", async () => {
    await openPlatform(({ backend }) => {
      backend.board = [toKandy(1, { callType: "Pass" }), second(), third()];
    });

    await screen.findByText("Next train does not stop here – stand back from the platform edge");
    expect(within(screen.getByRole("region", { name: "Then" })).getAllByRole("listitem")).toHaveLength(2);
  });
});

describe("a platform change away from this platform (US-D2 AC3, S-19, S-20)", () => {
  const moved = (hold: boolean) =>
    toKandy(9, {
      serviceNumber: "1520",
      platform: "5",
      previousPlatform: "1",
      hold,
      status: hold ? { kind: "Held", delayMinutes: 3 } : { kind: "OnTime", delayMinutes: 0 },
      time: "2026-10-20T01:50:00Z",
    });

  it("without a hold: 'Train 1520 to Kandy now departs from Platform 5' and 'Now departing from Platform 5' (S-20), no promise to wait", async () => {
    await openPlatform(({ backend }) => (backend.board = [moved(false), next()]));

    expect(await screen.findByText("Train 1520 to Kandy now departs from Platform 5")).toBeTruthy();
    expect(screen.getByText("Now departing from Platform 5")).toBeTruthy();
    expect(screen.queryByText(/will wait/)).toBeNull();
  });

  it("with a real hold: 'The train will wait for passengers at Platform 5' (S-19) and 'Held · 3 min late'", async () => {
    await openPlatform(({ backend }) => (backend.board = [moved(true), next()]));

    expect(await screen.findByText("Train 1520 to Kandy now departs from Platform 5")).toBeTruthy();
    expect(screen.getByText("The train will wait for passengers at Platform 5")).toBeTruthy();
    expect(screen.getByText("Held · 3 min late")).toBeTruthy();
    expect(screen.queryByText("Now departing from Platform 5")).toBeNull();
  });

  it("the moved train is no longer in the list of this platform", async () => {
    await openPlatform(({ backend }) => (backend.board = [moved(false), next()]));
    await screen.findByText("Train 1520 to Kandy now departs from Platform 5");

    const nextPanel = within(screen.getByRole("region", { name: "Next" }));
    expect(nextPanel.getAllByText("07:12").length).toBeGreaterThan(0);
    expect(screen.queryByRole("region", { name: "Then" })).toBeNull();
  });

  it("the notice is shown even when no other train is planned for the platform", async () => {
    await openPlatform(({ backend }) => (backend.board = [moved(false)]));

    expect(await screen.findByText("Train 1520 to Kandy now departs from Platform 5")).toBeTruthy();
    expect(screen.queryByText("No trains on this platform")).toBeNull();
  });

  it("a train that was moved TO this platform is not a notice here", async () => {
    await openPlatform(({ backend }) => (backend.board = [toKandy(1, { platform: "1", previousPlatform: "3" })]));

    await screen.findByRole("region", { name: "Next" });
    expect(screen.queryByText(/now departs from/)).toBeNull();
  });
});

describe("a hold on this platform", () => {
  it("the next train is held here: 'The train will wait for passengers at Platform 1'", async () => {
    await openPlatform(({ backend }) => {
      backend.board = [toKandy(1, { hold: true, status: { kind: "Held", delayMinutes: 2 } })];
    });

    expect(await screen.findByText("The train will wait for passengers at Platform 1")).toBeTruthy();
    expect(screen.getByText("Held · 2 min late")).toBeTruthy();
  });

  it("no hold, no such sentence anywhere", async () => {
    await openPlatform();
    await screen.findByRole("region", { name: "Next" });
    expect(screen.queryByText(/will wait/)).toBeNull();
    expect(screen.queryByText(/Now departing/)).toBeNull();
  });
});

describe("live updates and offline", () => {
  it("an event reads the board again: a new delay shows without a reload", async () => {
    const fake = await openPlatform();
    expect((await screen.findAllByText("On time")).length).toBeGreaterThan(0);

    fake.backend.board = [toKandy(1, { status: { kind: "Late", delayMinutes: 4 }, expectedTime: "2026-10-20T01:46:00Z" }), second(), third()];
    fake.stream.event("DelayChanged", {}, 11);
    await settle(600);

    expect(await screen.findByText("4 min late")).toBeTruthy();
  });

  it("when the next train changes, 'Calling at' follows it, and never shows the stations of the old one", async () => {
    const fake = await openPlatform();
    await screen.findByText("Polgahawela · Kandy");

    // The first train leaves; the next one only runs to Polgahawela.
    const short = journey({ journeyId: second().journeyId });
    short.stops = short.stops.filter((s) => s.name !== "Kandy");
    fake.backend.journeys.set(second().journeyId, short);
    fake.backend.board = [second(), third()];
    fake.stream.event("StopStatusChanged", {}, 12);
    await settle(600);

    expect(await screen.findByText("Polgahawela")).toBeTruthy();
    expect(screen.queryByText("Polgahawela · Kandy")).toBeNull();
  });

  it("offline: the banner, 'Offline', scheduled times with the word 'Scheduled', no expected time, no status, no moved-away notice", async () => {
    const fake = await openPlatform(({ backend }) => {
      backend.board = [
        toKandy(9, { serviceNumber: "1520", platform: "5", previousPlatform: "1", time: "2026-10-20T01:50:00Z" }),
        toKandy(1, { time: "2026-10-20T02:10:00Z", expectedTime: "2026-10-20T02:15:00Z", status: { kind: "Late", delayMinutes: 5 } }),
        second(),
      ];
    });
    await screen.findByText("Train 1520 to Kandy now departs from Platform 5");

    fake.backend.down = true;
    fake.stream.state.refuse = true;
    fake.stream.close();
    await settle(125_000);

    expect(screen.getByText("Live information unavailable – check announcements or ask staff")).toBeTruthy();
    expect(screen.getByText("Offline")).toBeTruthy();
    expect(screen.getAllByText("Scheduled").length).toBeGreaterThan(0);
    expect(screen.queryByText("5 min late")).toBeNull();
    expect(screen.queryByText("Expected")).toBeNull();
    expect(screen.queryByText(/now departs from/)).toBeNull();
    expect(screen.queryByText("On time")).toBeNull();
  });

  it("offline, a train whose scheduled time has passed is removed", async () => {
    const fake = await openPlatform(({ backend }) => {
      backend.board = [toKandy(1, { time: "2026-10-20T01:36:00Z" }), toKandy(2, { time: "2026-10-20T03:00:00Z" })];
    });
    await screen.findByRole("region", { name: "Next" });
    expect(screen.getAllByText("07:06").length).toBeGreaterThan(0);

    fake.backend.down = true;
    fake.stream.state.refuse = true;
    fake.stream.close();
    await settle(125_000);

    expect(screen.queryByText("07:06")).toBeNull();
    expect(screen.getAllByText("08:30").length).toBeGreaterThan(0);
  });
});

describe("addresses", () => {
  it("a platform display for a station that is not known says so", async () => {
    await openPlatform(undefined, "/platform/XYZ/1");
    expect(await screen.findByText("This station is not known.")).toBeTruthy();
  });

  it("the calling points are fetched for the right station (this one), not the whole journey", async () => {
    await openPlatform();
    await screen.findByText("Polgahawela · Kandy");
    // Maradana and Colombo Fort are before Gampaha on this journey: never listed.
    expect(screen.queryByText(/Maradana/)).toBeNull();
    expect(screen.queryByText(/Colombo Fort/)).toBeNull();
    expect(GAMPAHA).toBeTruthy();
    expect(KANDY).toBeTruthy();
    expect(VEYANGODA).toBeTruthy();
  });
});

// Rehearsal #3: a train that has not started has no live expected time, only its schedule.
describe("a train that has not started", () => {
  it("says 'Scheduled', not 'Expected', in the next-train panel and in the Then list", async () => {
    await openPlatform(({ backend }) => {
      backend.board = [
        toKandy(1, { status: { kind: "NotStarted", delayMinutes: null } }),
        toKandy(2, { time: "2026-10-20T02:00:00Z", expectedTime: "2026-10-20T02:00:00Z", status: { kind: "NotStarted", delayMinutes: null } }),
        third(),
      ];
    });

    const nextPanel = within(await screen.findByRole("region", { name: "Next" }));
    expect(nextPanel.getByText("Scheduled")).toBeTruthy();
    expect(nextPanel.queryByText("Expected")).toBeNull();

    const then = within(screen.getByRole("region", { name: "Then" }));
    // the unstarted train says Scheduled; the running (late) one still says Expected
    expect(then.getAllByText(/Scheduled/)).toHaveLength(1);
    expect(then.getAllByText(/Expected/)).toHaveLength(1);
  });

  it("a running train still says 'Expected'", async () => {
    await openPlatform();

    const nextPanel = within(await screen.findByRole("region", { name: "Next" }));
    expect(nextPanel.getByText("Expected")).toBeTruthy();
    expect(nextPanel.queryByText("Scheduled")).toBeNull();
  });
});
