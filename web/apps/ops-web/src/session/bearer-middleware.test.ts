import { describe, expect, it } from "vitest";
import { createApiClient } from "@smartrail/api-client";
import { bearerMiddleware } from "./bearer-middleware.ts";
import { Session } from "./session.ts";

const MINUTE = 60_000;

// A tiny server: sign-in and refresh hand out tokens, /staff/me records the Authorization header it received.
function setup() {
  let clock = Date.parse("2026-10-16T05:00:00Z");
  const seen: { authorization: (string | null)[] } = { authorization: [] };
  const token = (name: string) => new Response(JSON.stringify({ accessToken: name, expiresAt: new Date(clock + 15 * MINUTE).toISOString() }), { status: 200, headers: { "content-type": "application/json" } });
  const fetch = async (request: Request) => {
    const path = new URL(request.url).pathname;
    if (path.endsWith("/sign-in")) return token("token-1");
    if (path.endsWith("/refresh")) return token("token-2");
    seen.authorization.push(request.headers.get("authorization"));
    return new Response(JSON.stringify({ name: "Demo Controller", role: "Controller", stationId: null }), { status: 200, headers: { "content-type": "application/json" } });
  };
  const plain = createApiClient({ baseUrl: "http://localhost", fetch });
  const session = new Session({ api: plain, now: () => clock });
  const api = createApiClient({ baseUrl: "http://localhost", fetch });
  api.use(bearerMiddleware(session));
  return { session, api, seen, advance: (ms: number) => (clock += ms) };
}

describe("bearerMiddleware", () => {
  it("sends the access token as a Bearer header", async () => {
    const { session, api, seen } = setup();
    await session.signIn("CTRL-01", "pw");

    const { data } = await api.GET("/api/v1/staff/me");

    expect(seen.authorization).toEqual(["Bearer token-1"]);
    expect(data?.role).toBe("Controller");
  });

  it("sends no Authorization header when nobody is signed in", async () => {
    const { api, seen } = setup();

    await api.GET("/api/v1/staff/me");

    expect(seen.authorization).toEqual([null]);
  });

  it("refreshes an expiring token before the request leaves", async () => {
    const { session, api, seen, advance } = setup();
    await session.signIn("CTRL-01", "pw");
    advance(16 * MINUTE);
    session.noteActivity();

    await api.GET("/api/v1/staff/me");

    expect(seen.authorization).toEqual(["Bearer token-2"]);
  });
});
