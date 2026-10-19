import { render } from "@testing-library/react";
import { MemoryRouter } from "react-router";
import { App } from "../App.tsx";
import { createServices, ServicesProvider } from "../services.tsx";
import type { BoardRow, JourneyDetail } from "../types.ts";
import { journey, LINES } from "./data.ts";
import { fakeStream } from "./sse.ts";

// A fake backend for the page tests: the requests the pages make are answered from the fields below. The real typed client, the
// real feed and the real pages run; only the network is replaced.
// Every answer carries a `Date` header like a real server's: the fake's clock is the device clock plus `serverShiftMs`.
let serverShiftMs = 0;
export const json = (status: number, body: unknown) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json", date: new Date(Date.now() + serverShiftMs).toUTCString() },
  });

export function fakeBackend() {
  const backend = {
    lines: LINES as unknown,
    board: [] as BoardRow[],
    // Answers of GET /journeys/{id}, by id. A journey that is not listed answers 404.
    journeys: new Map<string, JourneyDetail>(),
    // While true, every request fails like a dead network.
    down: false,
    failLines: false,
    // While true, the board never answers (a slow network): the page is still waiting for its first data.
    hangBoard: false,
    requests: [] as Request[],
    // The server's clock is this far ahead of the device clock (negative: behind), in milliseconds.
    set serverShiftMs(ms: number) {
      serverShiftMs = ms;
    },
  };
  serverShiftMs = 0;
  const stream = fakeStream();

  const fetch = async (request: Request): Promise<Response> => {
    backend.requests.push(request);
    if (backend.down) throw new TypeError("network down");
    const path = new URL(request.url).pathname;
    if (path === "/api/v1/lines") return backend.failLines ? json(500, {}) : json(200, backend.lines);
    if (/^\/api\/v1\/stations\/[^/]+\/board$/.test(path)) return backend.hangBoard ? new Promise<Response>(() => {}) : json(200, backend.board);
    const match = /^\/api\/v1\/journeys\/([^/]+)$/.exec(path);
    if (match) {
      const found = backend.journeys.get(match[1]!);
      return found ? json(200, found) : json(404, { code: "NOT_FOUND", title: "Not found", status: 404 });
    }
    throw new Error(`The test did not expect a call to ${path}`);
  };

  const services = createServices({ baseUrl: "http://localhost", fetch, streamFetch: stream.fetch });
  const calls = (pattern: RegExp) => backend.requests.filter((r) => pattern.test(new URL(r.url).pathname));

  // The journey that goes with a board row, for "Calling at".
  const withJourney = (row: BoardRow) => backend.journeys.set(row.journeyId, journey({ journeyId: row.journeyId }));

  return { backend, stream, services, calls, withJourney };
}

export function renderApp(services: ReturnType<typeof createServices>, path: string) {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <ServicesProvider services={services}>
        <App />
      </ServicesProvider>
    </MemoryRouter>,
  );
}
