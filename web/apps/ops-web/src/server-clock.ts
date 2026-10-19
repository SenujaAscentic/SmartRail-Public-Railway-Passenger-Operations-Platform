import type { Middleware } from "@smartrail/api-client";

// The server's time, as far as the page can tell. Every API answer carries a `Date` header from the server's own clock (contract
// section 1), so the page keeps the difference between that and the device's clock. Ages ("updated 30 s ago") and the clock in the top
// bar are counted on THIS time, never on the device's: a device that is wrong, or a server on the demo clock, would otherwise show
// every value as "0 s ago" or "3 h ago". Display only: nothing is decided from it. The header has whole seconds, so ages are right to
// about a second.
export class ServerClock {
  private offsetMs = 0;

  now(): number {
    return Date.now() + this.offsetMs;
  }

  observe(response: Response): void {
    const header = response.headers.get("date");
    const server = header ? Date.parse(header) : Number.NaN;
    if (!Number.isNaN(server)) this.offsetMs = server - Date.now();
  }

  // For the API client: every answer teaches the clock.
  middleware(): Middleware {
    return {
      onResponse: ({ response }) => {
        this.observe(response);
        return response;
      },
    };
  }
}
