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

## Screens (19 Oct 2026, branch `feature/ops-web`)

Dashboard, journey page with the controller's decisions, platform change dialog, audit log. One line of reason per decision.

### Where the data comes from
- **The Main Line comes from `GET /lines` (the first line); `VITE_MAIN_LINE_ID` is no longer needed for the new screens.** The setting still exists for the bare `/live` events view.
- **`STREAM_PATH` is now `/api/v1/ops/stream`.** The ops screens need the SOURCE of each value (US-O3 star criterion), and only the staff stream sends it. It also ends when the access token expires; the existing reconnect logic gets a fresh token and carries on.
- **A snapshot gives a whole journey (`snapshot.journey` = what `GET /journeys/{id}` answers); an event only marks the journey "changed" and the page reads it again from the API.** The screens do not re-implement the backend's rules. Two events are applied at once because they are frequent and complete in themselves: `TrainPositionUpdated` (the position) and the source of a value.
- **Sources (Driver / GPS / Controller / Schedule) come from the staff stream:** `delaySource` and the source of each actual time in the snapshot, `source` on every event. A value whose source is not known yet says "unknown" instead of guessing.
- **Ages count from the time the page was told, with a one-second tick.** The server decides what is stale; the page only shows how old a value is.

### Wording and states
- **All status wording is in `status/status-text.ts` and the test runs every case of `shared/status-fixtures.json` against it** (CLAUDE.md section 5, step 6). The cases that belong to the passenger app and the displays are skipped by the test.
- **A held train says "Held · 3 min late" (S-08).** The API sends the hold apart from the status kind, so the page combines them; it never invents a delay.
- **Every state is icon + words + border + colour from the status tokens.**

### Dashboard (handoff 4b)
- **Alerts:** a running train whose position is Stale or Lost, Lost first, then the newest. Unacknowledged instructions and catering health are not built (crew messages and the provider panel are deferred).
- **Disrupted trains:** late, cancelled, terminated early or held; cancelled and terminated count as the worst, then the most late. On-time trains are not listed.
- **Line view:** a list of the stations of each direction, with each running train under the station it last passed, and the backend's "how far to the next station". A list, not a drawing: it reads in order with a screen reader and needs no layout code. The page positions nothing itself.
- **Filters:** station, and a section (from / to). A section keeps the journeys that go from one chosen station to the other, in that order.
- **A station manager sees the trains of their own station (the server decides that) and a notice that decisions are the controller's.**

### Journey page (handoff 4c) and decisions
- **Decisions are offered only to a controller (`GET /staff/me`).** The server checks every request anyway; hiding a button is courtesy.
- **Every decision sends the journey's version.** On `409 VERSION_CONFLICT` the page shows the server's words ("Changed by R. Perera 4 s ago – reload") and a Reload button that takes the journey from the 409 body.
- **Cancel, terminate and skip show a confirmation that lists the stations affected and name the passenger apps and information screens.** The `impact-preview` endpoint was never built, so the list is the journey's own remaining stops. Catering is not named: the journey detail does not know which stations have it.
- **The per-stop buttons have a hidden station name in their label** ("Platform… at Rambukkana"), so a screen reader can tell them apart.
- **Add stop and unskip are not on the page** (deferred, re-plan 2).
- **The GPS-contradicts-override warning (handoff 4c) is not built:** the backend sends no such flag yet.

### Platform change dialog (US-O5, handoff 4d)
- **Choose stop and new platform; the dialog asks the preview endpoint and shows the lead time in minutes and as one of three levels.** The lead time is the backend's number; the page never computes it.
- **Under 5 minutes: a warning in words and the hold option marked "(recommended)". It is not blocked** (decided by Senuja, 18 Oct).
- **"What people will see" shows two lines from the fixtures: the platform text (S-10) and the information-screen text (S-19 with a hold, S-20 without).** "The train will wait" appears only after the hold box is ticked (A-113); a test checks both.
- **A conflict is listed in words ("Platform 4 is expected to be occupied by 1520 at 18:04") and Confirm stays disabled until the person ticks that they have seen it.**
- **Only platforms in service, other than the current one, are offered** (`GET /stations/{id}`).
- **The lead time is as at the preview; the server works it out again at confirm.** The audit entry keeps the one the server used.

### Audit log (handoff 4g)
- **Filters: journey id, staff number, from, to. Times typed are Sri Lanka time and sent as UTC.** Newest first; names are shown only here, to the controller.

### Tests
- Status wording (20 fixtures), the journey store (9), the dashboard (6), the journey page and platform dialog (13), the audit log (4). The screens run against the fake backend; none has run against the real API in a browser.

### Open points (screens)
- **Not tried in a browser against the running backend yet.** The typed client, the stream parsing of the shared snapshot example and the backend's own tests are the safety net.
- **Source and age of a delay set before the page opened:** the age is taken from the latest actual stop time (the backend does not send when a controller delay was set). A controller delay shows no age until an event arrives.
- **The raw `/live` events page is a technical view and stays in the menu until the demo.**

## Accepted gaps, design alignment and the live check (20 Oct 2026)

### Gaps accepted by Senuja (20 Oct)
- **"GPS contradicts override" warning (handoff 4c): not built.** The backend sends no such flag, and it is not a MoSCoW Must.
- **Catering is not named in the confirmation dialog (cancel, terminate, skip).** The journey detail does not say which stations have catering. Revisit with the catering work.
- **The confirmation lists the journey's own remaining stops.** There is no `impact-preview` endpoint.
- **Fixed instead of accepted: the age of a controller delay.** The staff snapshot now carries `delaySetAt` and every `DelayChanged` has its own time, so the ops screens show "Controller override · 4 min ago" (US-O3 star criterion: every value has a source AND an age). The public payloads do not carry it.

### Aligned to docs/design/ops (component names as in the component sheet)
StatusBadge, SourceAgeLabel (`SourceAgeLabel.tsx`, with the "Controller override" wording), LiveIndicator and FreshnessBanner (`LiveIndicator.tsx`), AlertRow and DisruptedTrainRow (in `DashboardPage.tsx`), LineView with TrainMarker (one horizontal track, trains of the first route's direction above and the other direction below), StopRow (the Next stop is marked; per-row Source · age), ActionBar (`ActionButton`), ConfirmImpact in a Dialog (ConfirmImpactDialog), PlatformDialog with the platform buttons (PlatformChangeDialog with the LeadTimeNotice blocks), ConflictBanner (the version conflict) and the conflict block of the platform dialog, the audit table, the sign-in card, the sidebar with the user block and the top bar with the clock.
- **Colours come from the tokens.** The tokens `warning` / `onWarning` are not in `shared/tokens.json` yet: the stylesheet uses `--color-warning` / `--color-on-warning` when they exist and falls back to the late status colours with the normal text colour. Warning blocks keep dark text, an icon and words, never white text. Check again after the token commit.

### Designed, deferred (re-plan 2): NOT built
Messages to crew (sidebar item, message status, "Message driver", "Not acknowledged" alert, the crew line of the confirmation), station announcements (sidebar item and form), station reports (alert row and acknowledge), Add stop (button and screen), the driver and cab-tablet line of the journey header ("Driver Demo Driver 1 · Cab tablet signed in · online", last action, train set: the staff journey detail endpoint is deferred), the "Driver · cab" card of the platform preview (the crew instruction is a crew-messages feature), the Refused entries of the audit log (refused actions are not audited yet).

### Where the design and a rule or fixture differ (the rule wins; not guessed)
- **Information-screen text after a platform change:** the design shows "Train 1001 to Kandy now departs from Platform 5" and, with a hold, "The train will wait for passengers at Platform 5". The fixture S-20 says "Now departing from Platform 5" (no promise without a hold, A-113) and S-19 the wait text; the dialog uses the fixtures. The design's wording is the platform display's (US-D2 AC3).
- **A train whose position is lost (alert row):** the design says "No live data · GPS 14 min ago"; fixture S-16 says "Live location unavailable – scheduled times shown", which the alert shows.
- **"Inferred" as the source of a passed stop (design StopRow):** the backend records the source of the event that caused the inference, so the row shows that source. A separate "Inferred" label would need a backend flag.
- **Lead time shown as a sum (design: "17:55 − 17:52 = 3 min"):** the preview gives minutes only, so the dialog shows "expected arrival − now = 3 min". The page does not work out times.
- **Audit table (design):** the Journey column shows the number and name and the filters are dropdowns; ours shows a short id (the entry holds only the id) and text filters. The columns are otherwise the design's (Time, Staff, Role, Action, Journey, Reason) with a date added, because the log spans days.
- **Dialog focus:** focus moves into the dialog and Escape closes it, but Tab can still leave it (no full focus trap yet).

### Found in the live browser check (headless Chrome, real API, simulator on 1001) and fixed
- **Ages were all "0 s ago" on the demo clock:** the page counted ages from the device's clock, and the demo server is a day ahead. Ages and the clock in the top bar now count on the server's time, taken from the `Date` header of every answer (`server-clock.ts`); a test covers it.
- **A train at the end of the line was cut off, and markers sat at the wrong place** (the positions did not allow for the station columns). Fixed with room at both ends and positions measured from the middle of each station's column.
- **The platform dialog opened on the stop the train was standing at.** It now opens on the next scheduled stop.
- Console: only the expected 401 of the first silent refresh before sign-in, and a favicon 404 (fixed).
- **Repeat live check, 20 Oct (screenshots kept):** the top-bar clock showed the device's time (15:50) for the first 10 seconds after sign-in, because it only refreshed every 10 s and was first computed before any answer had taught the page the server's time. It now ticks every second (`useNow.test.tsx`); on the demo clock it shows 06:31 within seconds.

- **Dialog focus trap (20 Oct).** Tab and Shift+Tab stay inside the dialog and wrap at both ends (disabled controls are skipped), Escape closes it, and focus returns to the button that opened it. Done in the one `Dialog` component, so every decision dialog has it; it takes focus once, when it opens (see rehearsal bug #6).

- **Live events page: the Main Line comes from `GET /lines` (20 Oct).** The first line is used (there is one); `VITE_MAIN_LINE_ID` stays as an optional override (a value that is not a GUID is still refused). The same rule as the dashboard.

- **"Today's trains" on the dashboard (20 Oct).** Checked first: a train nobody had started was on no list (not disrupted, not on the line view), so the controller could not open it. The new panel lists every journey the stream holds (today's service day, plus any still running from yesterday), both directions, by scheduled departure from its origin, with train, route, status and an Open link. It uses the same snapshot as the rest of the page, so no new endpoint. The station and section filters apply to it too.

- **Line view: a pin ON the line (20 Oct).** Each running train with a position also gets a small pin on the track itself (train icon, service number, the status colour), at the same place as its card (last station, next station, fraction from the backend). The cards above and below stay and keep the details and the link; the pins are `aria-hidden` because they would only repeat the card. Not checked in a browser yet: Vitest checks the pin, its colour class, icon, number and its 25% place, but the CSS (pin centred on the line) is only reasoned, not seen.
