import { describe, expect, it } from "vitest";
import { createServerClock } from "./services.tsx";

describe("createServerClock", () => {
  const reply = (date: string | null) => new Response("", { headers: date ? { date } : {} });

  it("starts with the device time and follows the Date header of the answers", () => {
    let device = Date.parse("2026-10-20T10:00:00Z");
    const clock = createServerClock(() => device);
    expect(clock.serverNow()).toBe(device);

    clock.learnFrom(reply("Tue, 20 Oct 2026 01:00:00 GMT")); // the server is 9 hours behind
    expect(clock.serverNow()).toBe(Date.parse("2026-10-20T01:00:00Z"));

    device += 30_000; // the device clock ticks on between answers
    expect(clock.serverNow()).toBe(Date.parse("2026-10-20T01:00:30Z"));
  });

  it("ignores an answer without a usable Date header", () => {
    const clock = createServerClock(() => 1_000_000);
    clock.learnFrom(reply("Tue, 20 Oct 2026 01:00:00 GMT"));
    const before = clock.serverNow();
    clock.learnFrom(reply(null));
    clock.learnFrom(reply("not a date"));
    expect(clock.serverNow()).toBe(before);
  });
});
