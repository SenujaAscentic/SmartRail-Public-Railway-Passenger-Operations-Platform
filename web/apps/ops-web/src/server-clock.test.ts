import { describe, expect, it } from "vitest";
import { ServerClock } from "./server-clock.ts";

const answer = (date: string | null) => new Response(null, { status: 200, headers: date ? { date } : {} });

describe("the server's clock as the page knows it", () => {
  it("follows the Date header of an answer, so a device clock that is far off does not matter", () => {
    const clock = new ServerClock();
    const serverNow = Date.now() + 26 * 60 * 60 * 1000;   // a demo server one day and two hours ahead of this device

    clock.observe(answer(new Date(serverNow).toUTCString()));

    expect(Math.abs(clock.now() - serverNow)).toBeLessThan(2000);
  });

  it("keeps the device's time until it has heard from the server, and ignores an answer without a usable date", () => {
    const clock = new ServerClock();
    expect(Math.abs(clock.now() - Date.now())).toBeLessThan(50);

    clock.observe(answer(null));
    clock.observe(answer("not a date"));

    expect(Math.abs(clock.now() - Date.now())).toBeLessThan(50);
  });

  it("learns from every answer of the API client through its middleware", async () => {
    const clock = new ServerClock();
    const serverNow = Date.now() - 5 * 60 * 1000;
    const middleware = clock.middleware();

    await middleware.onResponse?.({ response: answer(new Date(serverNow).toUTCString()) } as never);

    expect(Math.abs(clock.now() - serverNow)).toBeLessThan(2000);
  });
});
