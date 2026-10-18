import type { Middleware } from "@smartrail/api-client";
import type { Session } from "./session.ts";

// For every request of the app's API client: put the current access token in the Authorization header,
// refreshing it first when it is about to expire. Sign-in, refresh and sign-out are NOT made through this client
// (the session uses a plain one), so there is no loop.
export function bearerMiddleware(session: Session): Middleware {
  return {
    async onRequest({ request }) {
      const token = await session.getAccessToken();
      if (token) request.headers.set("Authorization", `Bearer ${token}`);
      return request;
    },
  };
}
