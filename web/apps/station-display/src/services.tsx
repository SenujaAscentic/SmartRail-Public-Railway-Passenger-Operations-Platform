import { createApiClient, type ApiClient, type ApiClientOptions } from "@smartrail/api-client";
import { createContext, useContext, type ReactNode } from "react";

// What the pages need: a typed API client (no token: the display pages only use the public, read-only endpoints), the
// event stream's fetch, and the clock. Tests replace all three.
export interface Services {
  api: ApiClient;
  // The stream is read with fetch (see feed/stream.ts). Tests replace it.
  streamFetch: typeof globalThis.fetch;
  now: () => number;
}

export function createServices(options: ApiClientOptions & { now?: () => number; streamFetch?: typeof globalThis.fetch } = {}): Services {
  const { now, streamFetch, ...clientOptions } = options;
  return {
    api: createApiClient(clientOptions),
    streamFetch: streamFetch ?? ((input, init) => globalThis.fetch(input, init)),
    now: now ?? (() => Date.now()),
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
