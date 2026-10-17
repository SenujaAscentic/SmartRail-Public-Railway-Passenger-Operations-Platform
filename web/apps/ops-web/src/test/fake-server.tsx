import { render } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { App } from "../App.tsx";
import { ServicesProvider } from "../services-context.tsx";
import { createServices, type Services } from "../services.ts";
import { sse, type Responder } from "./sse.ts";

// A fake backend for the screen tests: the requests the app makes are answered from `routes`, keyed "METHOD /path".
// The real session, the real typed client and the real middleware run; only the network is replaced.
export type Routes = Record<string, () => Response | Promise<Response>>;

export const SIGN_IN = "POST /api/v1/auth/staff/sign-in";
export const REFRESH = "POST /api/v1/auth/refresh";
export const SIGN_OUT = "POST /api/v1/auth/sign-out";
export const ME = "GET /api/v1/staff/me";
export const MINUTE = 60_000;

export const json = (status: number, body: unknown, headers: Record<string, string> = {}) =>
  new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json", ...headers } });

export const refused = () => json(401, { code: "UNAUTHENTICATED", title: "Sign in required", status: 401, requestId: "r" });

export function fakeBackend(overrides: Routes = {}) {
  const clock = { now: Date.parse("2026-10-16T05:00:00Z") };
  const requests: Request[] = [];
  const tokenResponse = (name: string) => json(200, { accessToken: name, expiresAt: new Date(clock.now + 15 * MINUTE).toISOString() });

  const routes: Routes = {
    [REFRESH]: refused,   // by default nobody is signed in
    [SIGN_IN]: () => tokenResponse("token-1"),
    [SIGN_OUT]: () => new Response(null, { status: 204 }),
    [ME]: () => json(200, { name: "Demo Controller", role: "Controller", stationId: null }),
    ...overrides,
  };

  const fetch = async (request: Request): Promise<Response> => {
    requests.push(request);
    const key = `${request.method} ${new URL(request.url).pathname}`;
    const route = routes[key];
    if (!route) throw new Error(`The test did not expect a call to ${key}`);
    return route();
  };

  // The event stream: tests set `stream.responder`; by default it is open and quiet. `signals` shows whether the page closed it.
  const stream = { responder: sse("", { hold: true }) as Responder, requests: [] as { url: string; headers: Record<string, string> }[], signals: [] as AbortSignal[] };
  const streamFetch = async (input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> => {
    stream.requests.push({ url: String(input), headers: { ...(init.headers as Record<string, string>) } });
    if (init.signal) stream.signals.push(init.signal);
    return stream.responder(init);
  };

  const services: Services = createServices({ baseUrl: "http://localhost", fetch, now: () => clock.now, streamFetch });
  const calls = (key: string) => requests.filter((r) => `${r.method} ${new URL(r.url).pathname}` === key);

  return { services, routes, clock, requests, calls, tokenResponse, stream };
}

export function renderApp(services: Services, path = "/") {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <ServicesProvider services={services}>
        <App />
      </ServicesProvider>
    </MemoryRouter>,
  );
}
