# ops-web: design decisions and open points

Written 17 Oct 2026 at the end of the skeleton work (branch `feature/ops-web-skeleton`). One line of reason per decision.
The backend code guide and AI log are not touched here (agent B owns them); this file is where the ops-web decisions live until they are moved.

## Design decisions

### Structure and tooling
- **npm workspace in `web/`, exact version pins, `ignore-scripts=true`, `package-lock.json` committed.** Reproducible installs, and no dependency can run code at install time.
- **`api-client` and `tokens` are TypeScript source, with no build step.** Vite compiles them with the app, so there is less to explain and to break.
- **Node scripts are `.ts` run by Node 24 itself.** No runner package (tsx, ts-node) is needed.
- **TypeScript is pinned to 5.9.3, not the newest 7.x.** `openapi-typescript` 7.13 declares the peer `typescript ^5.x`.
- **`overrides` pins `react` and `react-dom` to 19.3.0 in `web/package.json`.** `@testing-library/react` and `react-router` pulled in React 19.3.1 as a second copy, which broke hooks ("Cannot read properties of null (reading 'useEffect')") and was newer than the approved version.
- **Manifests were edited by hand and installed once.** `npm install -w <workspace> <pkg>` kept dropping entries added by an earlier install.
- **Dev server proxies `/api` to the backend (`BACKEND_URL`, default `http://localhost:5045`).** The staff refresh cookie is HttpOnly and SameSite=Strict, so the page and the API must share one origin; this also needs no CORS.
- **Design tokens: `shared/tokens.json` becomes `dist/tokens.css` before dev, build, typecheck and test; it is not committed.** One source for web and Flutter, and a generated file cannot drift from it.
- **Font is a stack (`"Noto Sans", system-ui, sans-serif`), no font package.** The tokens ask for Noto Sans; adding a package for it was not approved.
- **Plain CSS on the tokens, no component library yet.** The skeleton has one form and one table; Mantine or MUI can come with the dashboard.

### API types
- **Types are generated from `backend/openapi.yaml` (`npm run api:generate`) and the file is committed.** C-8: no hand-written API types; the diff shows what changed.
- **`api:check` and a test fail when `schema.d.ts` is out of date; CI runs `api:check`.** A backend API change cannot be merged without the matching regenerated types.
- **Type tests use `@ts-expect-error` lines.** `npm run typecheck` fails if the client ever stops rejecting a wrong path or field.

### Session, refresh and idle (`src/session/session.ts`)
- **The access token lives in memory only.** A script in the page cannot find it in storage (threat T-14); a test checks localStorage, sessionStorage and cookies stay empty.
- **The refresh token is the HttpOnly cookie, never read by the app.** The browser sends it by itself to `/api/v1/auth/*`.
- **On page load the app tries one silent `POST /auth/refresh`.** The memory token is gone after a reload; a good cookie signs the person back in without a message.
- **There is no refresh timer.** A token is fetched only when something needs one (an API call, a stream connect), so an unattended screen stops refreshing and the session can end.
- **Refresh only happens for a person who was active in the last 30 minutes (pointer, key, scroll).** Otherwise the session ends as "idle" and the refresh cookie is revoked with a best-effort sign-out. This is your chosen idle rule.
- **The 10-minute activity refresh (addition beyond the agreed rule).** The server's idle clock only moves on a refresh; without this, someone who is watching the live page but not clicking would be refused after 30 minutes although they were present. When there is activity and the last refresh is 10 minutes old, the app refreshes in the background; it is not a timer, so an unattended screen still goes idle. Remove `noteActivity`'s refresh branch to go back to pure on-demand.
- **A token that expires within 30 seconds counts as expired.** A request never leaves with a token that dies in flight.
- **Concurrent callers share one refresh.** The server lets a refresh token be used once (plus a 10-second grace); three simultaneous refreshes would look like a copied token and end the session.
- **Only a 401 on refresh ends the session ("expired").** A 500 or a dropped connection says nothing about the session, so it is kept and tried again.
- **`restore()` runs once and never overwrites an ended-session message.** Otherwise the page-load check would wipe the "you were away" notice.
- **Sign-in, refresh and sign-out use a plain API client; everything else uses a client with the Bearer middleware.** The auth calls must not wait for a token from themselves.
- **Sign-out is best effort on the network but always clears the app.** A person who clicks "Sign out" is signed out here even if the server cannot be reached.

### Sign-in screen
- **Every wrong credential gives the same message.** Contract section 6: sign-in failures never say which part was wrong.
- **`429 SIGN_IN_LOCKED` is its own message with the wait in minutes (from `Retry-After`).** Retrying cannot help, so the person must be told differently. Any other 429 is not called a lock.
- **Required fields are checked in the page and by the server.** The person sees the problem at once; the server still never trusts the page.
- **Errors are text next to the field (`aria-describedby`, `aria-invalid`) plus one summary message above the button.** Screen readers read the field message with the field; only the summary is a live region, so nothing is read twice.
- **On a failed attempt the password is cleared and focus goes to it; on a missing field focus goes to the first problem.** Keyboard users land where they must act.
- **Every message has an icon, words (with a hidden "Error:" / "Notice:") and a border.** Status is never colour alone (tokens rule, Q-4).

### Live page and SSE client (`src/stream/`)
- **`fetch` with `@microsoft/fetch-event-source`, not `EventSource`.** `EventSource` cannot send an Authorization header, and a token in the URL leaks into logs (contract section 2, C-6).
- **The token is fetched before every (re)connect and sent in a header; the URL holds only `lineIds`.** A long-lived page never reuses an expired token, and nothing secret is in a URL.
- **The path is one constant (`STREAM_PATH = /api/v1/stream`).** `/api/v1/ops/stream` replaces it later by changing one line.
- **`openWhenHidden: true`.** A control-room screen in a background tab must keep receiving; the library pauses hidden tabs by default.
- **When the server ends the stream, the client reconnects.** A stream is meant to stay open; the library then sends `Last-Event-ID` so the server replays only what was missed.
- **Retry delay is 1 s, 2 s, 3 s … never above 5 s.** The picture should come back quickly and one stream is cheap to reopen.
- **A refused request (4xx other than 401, 408, 429) is not retried.** The server would refuse it again (for example 400, no filter); the page shows "Not connected" with the reason.
- **A 401 gets one retry with a fresh token; a second 401 in a row stops.** The first may only mean the token just expired; looping would hammer the server.
- **Heartbeat comments are dropped.** This library version reports `: keep-alive` as an empty message instead of skipping it, which would add a blank event every 15 seconds.
- **The page reads only the envelope fields (`sequence`, `occurredAt`, `journeyId`); the data stays opaque JSON shown as plain text in a `<pre>`.** Decision 3 of the plan: no hand-written event types until events have a schema; plain text means a note can never become markup (CLAUDE.md section 6).
- **A `resync` clears the list and shows "the view was reloaded".** What is on screen may be stale; the snapshots that follow refill it.
- **The list keeps the 50 newest events.** It is a live view, not a log.
- **Times are shown in Asia/Colombo, built from `Intl` parts.** Stored and sent in UTC; a different browser locale cannot reorder the text.
- **The connection badge uses the shared status tokens: Live = OnTime style, Reconnecting = Stale, Not connected = Lost.** The same look SmartRail gives those meanings elsewhere, with an icon and words as well.
- **The hook ignores anything an already-aborted connection still reports.** React's development double-run and leaving the page would otherwise flash a wrong "closed" state.

### Main Line id
- **`VITE_MAIN_LINE_ID` (in `.env.local`, see `.env.example`) is a development-only setting until `GET /lines` exists.** The stream needs a `lineIds` filter and line ids are generated per database.
- **The value must be a GUID, lower-cased, or the page refuses to connect.** Nothing odd ever ends up in a URL.
- **How to read it: `SELECT "Id" FROM network."Lines";` after the backend has started once.** Written in the README.

### Tests
- **Screen tests run the real session, the typed client and the Bearer middleware against a fake backend; only the network is replaced.** They exercise the real wiring.
- **The key tests were proven able to fail** by breaking the code on purpose and watching the matching tests go red: idle gate, memory-only storage, shared refresh, 500-on-refresh, restore guard; heartbeat filter, reconnect, 400 retry, resync, token in the URL.
- **`shared-events.test.ts` checks every `shared/events/*.json` that exists and passes with zero files.** New examples are covered the moment they land; `SHARED_EVENTS_DIR` lets it run against another folder (it passed against agent B's five examples).
- **CI job `web`: `npm ci`, `api:check`, typecheck, test, build.** `actions/setup-node` is pinned by commit SHA (v7.0.0) like the other actions.

## Open points

- **Not verified against a running backend.** Everything is tested against fakes. The backend `/stream` is on agent B's branch, not yet on `develop`; until it merges the live page shows "Not connected" with a refusal. Manual check once both run: start the backend, set `VITE_MAIN_LINE_ID`, sign in as `CTRL-01`, open `/live`.
- **Snapshot and `resync` names and shapes come from agent B's examples** (`shared/events/` on `feature/outbox-stream`), not from `develop`. Confirm after the merge; the events test will then cover them.
- **`@microsoft/fetch-event-source` is stable but unmaintained** (official Microsoft package, last release April 2021, zero dependencies). If it ever misbehaves, the replacement is a small fetch-and-parse loop of about 60 lines; the tests would stay.
- **The stream keeps running on an expired session.** The server does not re-check a token while a stream is open, so an unattended screen keeps receiving data after its session would have ended. Question for agent B when `/ops/stream` is built: close the stream when the access token expires, so a reconnect has to pass the idle rule.
- **The ops stream will need a signed-in person.** The 401 handling (one retry with a fresh token, then stop) is tested only with fakes.
- **The 10-minute activity refresh is a judgement call** (see above); confirm or remove.
- **Secure cookie on `http://localhost`:** Chrome and Firefox accept it; Safari does not. The demo uses Chrome.
- **`@microsoft/fetch-event-source`, CI pin dates:** the `actions/setup-node` v7.0.0 release date could not be checked (no `gh`); please confirm it meets the one-week rule.
- **Not built (out of scope here):** the dashboard, station-display, Playwright E2E, `GET /lines` use, role-based screens for station managers.
