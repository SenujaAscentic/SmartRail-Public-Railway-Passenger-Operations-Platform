import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";
import { createApiClient, type components } from "../src/index.ts";
import { generateSchemaText, isUpToDate, schemaPath } from "../scripts/generate-schema.ts";

describe("the generated schema", () => {
  // The same check as `npm run api:check`: someone changed backend/openapi.yaml but did not regenerate.
  it("matches backend/openapi.yaml", async () => {
    const committed = readFileSync(schemaPath, "utf8");
    expect(isUpToDate(await generateSchemaText(), committed), "run `npm run api:generate` in web/packages/api-client").toBe(true);
  });

  it("ignores line endings and a missing final newline when comparing", () => {
    expect(isUpToDate("a\r\nb\r\n", "a\nb")).toBe(true);
    expect(isUpToDate("a\nb\n", "a\nc\n")).toBe(false);
  });

  it("describes the endpoints the ops web uses", () => {
    const text = readFileSync(schemaPath, "utf8");
    for (const path of ["/api/v1/auth/staff/sign-in", "/api/v1/auth/refresh", "/api/v1/auth/sign-out", "/api/v1/staff/me"]) {
      expect(text).toContain(`"${path}"`);
    }
  });
});

// A fake server: records the request the client builds and answers with the given JSON.
function fakeServer(status: number, body: unknown) {
  const seen: { request?: Request } = {};
  const fetch = async (request: Request) => {
    seen.request = request;
    return new Response(JSON.stringify(body), { status, headers: { "content-type": "application/json" } });
  };
  // A browser resolves a relative URL against the page; Node has no page, so the tests give an explicit origin.
  return { seen, client: createApiClient({ baseUrl: "http://localhost", fetch }) };
}

describe("createApiClient", () => {
  it("sends a typed sign-in as JSON to the path from openapi.yaml, on the same origin", async () => {
    const answer: components["schemas"]["AccessTokenResponse"] = { accessToken: "a.b.c", expiresAt: "2026-10-16T05:00:00Z" };
    const { seen, client } = fakeServer(200, answer);

    const { data, error } = await client.POST("/api/v1/auth/staff/sign-in", { body: { staffNumber: "CTRL-01", password: "secret" } });

    expect(error).toBeUndefined();
    expect(data?.accessToken).toBe("a.b.c");
    expect(seen.request?.method).toBe("POST");
    expect(new URL(seen.request!.url).pathname).toBe("/api/v1/auth/staff/sign-in");
    expect(seen.request?.headers.get("content-type")).toContain("application/json");
    expect(await seen.request!.json()).toEqual({ staffNumber: "CTRL-01", password: "secret" });
  });

  it("hands back the error body of a non-2xx answer instead of throwing", async () => {
    const problem = { title: "Sign-in is locked", status: 429, code: "SIGN_IN_LOCKED", requestId: "r1" };
    const { client } = fakeServer(429, problem);

    const { data, error, response } = await client.POST("/api/v1/auth/staff/sign-in", { body: { staffNumber: "SM-KDT", password: "x" } });

    expect(data).toBeUndefined();
    expect(response.status).toBe(429);
    expect(error).toMatchObject({ code: "SIGN_IN_LOCKED" });
  });
});

// Compile-time checks: `npm run typecheck` fails if the types stop catching these mistakes. Never called.
export function _typeChecksOnly() {
  const client = createApiClient();
  // @ts-expect-error a path that is not in openapi.yaml
  void client.GET("/api/v1/no-such-endpoint");
  // @ts-expect-error a field that is not in the sign-in request
  void client.POST("/api/v1/auth/staff/sign-in", { body: { staffNumber: "x", pasword: "typo" } });
}
