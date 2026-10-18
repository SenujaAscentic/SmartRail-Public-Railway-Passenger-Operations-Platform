import createClient from "openapi-fetch";
import type { paths } from "./schema";

// The generated description of the API: `paths` (every URL and its request and response) and `components` (named schemas).
// Apps take their types from here, e.g. components["schemas"]["AccessTokenResponse"].
export type { components, paths } from "./schema";
// For apps that add middleware to the client (for example the ops web puts the Bearer token on every request).
export type { Middleware } from "openapi-fetch";

export interface ApiClientOptions {
  // "" (the default) means the same origin as the page. In development the Vite dev server proxies /api to the backend,
  // so the app and the API share one origin and the HttpOnly refresh cookie works without CORS.
  baseUrl?: string;
  // For tests, and for apps that need to wrap fetch. Defaults to the global fetch.
  fetch?: (request: Request) => Promise<Response>;
}

// One typed client for the whole API. Every call is checked against openapi.yaml at build time:
// a renamed field or a wrong path fails `npm run typecheck`, not the demo.
export function createApiClient(options: ApiClientOptions = {}) {
  return createClient<paths>({
    baseUrl: options.baseUrl ?? "",
    ...(options.fetch ? { fetch: options.fetch } : {}),
  });
}

export type ApiClient = ReturnType<typeof createApiClient>;
