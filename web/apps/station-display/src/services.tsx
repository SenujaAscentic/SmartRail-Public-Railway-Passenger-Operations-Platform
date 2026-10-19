import { createApiClient, type ApiClient, type ApiClientOptions } from "@smartrail/api-client";
import { createContext, useContext, type ReactNode } from "react";

// What the pages need: a typed API client (no token: the display pages only use the public, read-only endpoints), the
// event stream's fetch, and the clock. Tests replace all three.
export interface Services {
  api: ApiClient;
  // The stream is read with fetch (see feed/stream.ts). Tests replace it.
  streamFetch: typeof globalThis.fetch;
  // The device clock: only for measuring how long ago something happened (contact, timers).
  now: () => number;
  // The SERVER's time in milliseconds: the device clock plus the difference learned from the `Date` header of the answers.
  // The header clock, "scheduled" times and removing trains by the clock use this one (a display's own clock may be wrong).
  serverNow: () => number;
}

// Remembers how far the server's clock is from the device clock. Every answer carries a `Date` header (whole seconds), so
// the offset is renewed with every load and with every (re)connect of the stream; between them the device clock ticks.
export function createServerClock(deviceNow: () => number) {
  let offset = 0;
  return {
    serverNow: () => deviceNow() + offset,
    // Reads the `Date` header of an answer (a missing or unreadable one changes nothing).
    learnFrom(response: Response): void {
      const header = response.headers.get("date");
      const server = header === null ? Number.NaN : Date.parse(header);
      if (!Number.isNaN(server)) offset = server - deviceNow();
    },
  };
}

export function createServices(options: ApiClientOptions & { now?: () => number; streamFetch?: typeof globalThis.fetch } = {}): Services {
  const { now, streamFetch, ...clientOptions } = options;
  const deviceNow = now ?? (() => Date.now());
  const clock = createServerClock(deviceNow);

  // Both the API calls and the stream pass through a wrapper that reads the `Date` header of the answer.
  const apiFetch = clientOptions.fetch ?? ((request: Request) => globalThis.fetch(request));
  const baseStreamFetch = streamFetch ?? ((input: RequestInfo | URL, init?: RequestInit) => globalThis.fetch(input, init));
  return {
    api: createApiClient({
      ...clientOptions,
      fetch: async (request) => {
        const response = await apiFetch(request);
        clock.learnFrom(response);
        return response;
      },
    }),
    streamFetch: async (input, init) => {
      const response = await baseStreamFetch(input, init);
      clock.learnFrom(response);
      return response;
    },
    now: deviceNow,
    serverNow: clock.serverNow,
  };
}

const ServicesContext = createContext<Services | null>(null);

export function ServicesProvider({ services, children }: { services: Services; children: ReactNode }) {
  return <ServicesContext.Provider value={services}>{children}</ServicesContext.Provider>;
}

export function useServices(): Services {
  const services = useContext(ServicesContext);
  if (!services) throw new Error("useServices must be used inside <ServicesProvider>.");
  return services;
}
