import { createApiClient, type ApiClient, type ApiClientOptions } from "@smartrail/api-client";
import { bearerMiddleware } from "./session/bearer-middleware.ts";
import { ServerClock } from "./server-clock.ts";
import { Session } from "./session/session.ts";

// The two things the screens need: the session, and an API client that sends its access token.
export interface Services {
  session: Session;
  api: ApiClient;
  // The stream is read with fetch (not EventSource, which cannot send an Authorization header). Tests replace it.
  streamFetch: typeof globalThis.fetch;
  // The server's time (from the Date header of its answers), for ages and the clock on screen.
  clock: ServerClock;
}

export function createServices(options: ApiClientOptions & { now?: () => number; streamFetch?: typeof globalThis.fetch } = {}): Services {
  const { now, streamFetch, ...clientOptions } = options;

  // The session talks to sign-in, refresh and sign-out through a PLAIN client: those calls must not go through the
  // bearer middleware (which asks the session for a token), or they would wait for themselves.
  const session = new Session({ api: createApiClient(clientOptions), ...(now ? { now } : {}) });

  const clock = new ServerClock();
  const api = createApiClient(clientOptions);
  api.use(bearerMiddleware(session));
  api.use(clock.middleware());

  // The stream's answers carry the server's time too.
  const baseStreamFetch = streamFetch ?? ((input: RequestInfo | URL, init?: RequestInit) => globalThis.fetch(input, init));
  const timedStreamFetch: typeof globalThis.fetch = async (input, init) => {
    const response = await baseStreamFetch(input, init);
    clock.observe(response);
    return response;
  };
  return { session, api, streamFetch: timedStreamFetch, clock };
}
