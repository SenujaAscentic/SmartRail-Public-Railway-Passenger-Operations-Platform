import { createApiClient, type ApiClient, type ApiClientOptions } from "@smartrail/api-client";
import { bearerMiddleware } from "./session/bearer-middleware.ts";
import { Session } from "./session/session.ts";

// The two things the screens need: the session, and an API client that sends its access token.
export interface Services {
  session: Session;
  api: ApiClient;
}

export function createServices(options: ApiClientOptions & { now?: () => number } = {}): Services {
  const { now, ...clientOptions } = options;

  // The session talks to sign-in, refresh and sign-out through a PLAIN client: those calls must not go through the
  // bearer middleware (which asks the session for a token), or they would wait for themselves.
  const session = new Session({ api: createApiClient(clientOptions), ...(now ? { now } : {}) });

  const api = createApiClient(clientOptions);
  api.use(bearerMiddleware(session));
  return { session, api };
}
