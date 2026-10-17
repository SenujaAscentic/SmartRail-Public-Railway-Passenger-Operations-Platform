# SmartRail web

npm workspace (ADR-06). Node 24 or newer.

| Folder | What |
|---|---|
| `apps/ops-web` | The operations web app (staff only). |
| `packages/api-client` | Typed API calls. Types are **generated** from `backend/openapi.yaml`. |
| `packages/tokens` | `shared/tokens.json` turned into CSS variables (generated, not committed). |

## Run it

```
cd web
npm install
npm run dev -w @smartrail/ops-web      # http://localhost:5173
```

Start the backend too (`dotnet run --project backend/src/SmartRail.Api`, it listens on `http://localhost:5045`).
The dev server answers `/api` itself and forwards it to the backend, so the page and the API share one origin
(the staff refresh cookie is HttpOnly and SameSite=Strict, so it needs that). Another backend address: set `BACKEND_URL`.

## The live page needs the Main Line's id (development only)

The stream requires a filter, and the live page asks for the Main Line (`lineIds`). Until the backend has `GET /lines`, the id is a
development setting: copy `apps/ops-web/.env.example` to `apps/ops-web/.env.local`, set `VITE_MAIN_LINE_ID`, restart the dev server.
Read the id from the database once the backend has started: `SELECT "Id" FROM network."Lines";`.

## Checks

```
npm run typecheck      # all workspaces
npm test               # all workspaces
npm run api:check -w @smartrail/api-client     # fails when src/schema.d.ts is out of date
```

After the backend API changes: run the backend tests with `UPDATE_OPENAPI=1`, then
`npm run api:generate -w @smartrail/api-client`, and commit the changed `schema.d.ts`.

Every dependency is pinned to an exact version and install scripts are off (`web/.npmrc`).

CI (`.github/workflows/ci.yml`, job `web`) runs `npm ci`, `api:check`, `typecheck`, `test` and `build`.
