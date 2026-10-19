import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ServicesProvider } from "./services-context.tsx";
import { ServerClock } from "./server-clock.ts";
import type { Services } from "./services.ts";
import { useNow } from "./useNow.ts";

function Now() {
  return <p>{new Date(useNow(1000)).toISOString()}</p>;
}

describe("the time the screens count on", () => {
  beforeEach(() => vi.useFakeTimers({ now: new Date("2026-10-19T10:00:00Z") }));
  afterEach(() => vi.useRealTimers());

  it("moves to the server's time within a second of the first answer, not ten seconds later", () => {
    const clock = new ServerClock();
    const services = { clock, session: { restore: async () => {}, noteActivity: () => {} } } as unknown as Services;
    render(<ServicesProvider services={services}><Now /></ServicesProvider>);
    expect(screen.getByText("2026-10-19T10:00:00.000Z")).toBeTruthy();   // before any answer: the device's time

    clock.observe(new Response(null, { headers: { date: new Date("2026-10-20T00:30:00Z").toUTCString() } }));   // the demo server is a day ahead
    act(() => { vi.advanceTimersByTime(1000); });

    expect(screen.getByText("2026-10-20T00:30:01.000Z")).toBeTruthy();
  });
});
