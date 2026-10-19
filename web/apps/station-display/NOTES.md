# station-display: design decisions and open points

Written 19 Oct 2026 (branch `feature/station-display`). One line of reason per decision. Design: `docs/design/display/`; token values win over pixels.

## The pages

| Address | Page | Story |
|---|---|---|
| `/board/FOT` | Main departures board: next 10 departures, one column for each direction | US-D1 |
| `/platform/FOT/5` | Platform display: next train + the stations it still calls at + the following two | US-D2 |

The station in the address is its **code** (FOT, GPH, KDT, any case) or its **id**. It is found from `GET /lines`, which lists every station with id, code and name. Public, read-only endpoints only: no token is sent (a test checks it). Development: `npm run dev -w @smartrail/station-display` (port 5174, `/api` goes to the backend).

## Design decisions

### Data
- **The data is read again after ANY stream event, after every (re)connect, and by a poll every 30 s while the stream is down.** The display never works out a change itself (same design as the passenger app). A burst of events causes one load (300 ms pause).
- **"In contact" means a load worked or bytes arrived (an event or the 15-second heartbeat).** Offline = no contact for 2 minutes. A poll that works keeps the display online although the stream is down (the backend can still be reached).
- **No bytes for 45 s closes the connection and connects again.** A dead connection does not announce itself.
- **The stream client is a copy of ops-web's idea, not shared code.** Another package was not approved; the two differ (no token, `stationIds`, silence rule). It can move to a shared package later.
- **Board request: `kind=departures&limit=30`, shown: the trains that stop here, at most 10.** The backend also lists trains that only pass (`callType` Pass), which the platform display needs; a limit of 10 could be used up by them.
- **Columns come from `GET /lines`**: a train belongs to the route (Fort to Kandy, or back) on which its destination lies after this station; the heading is the route's last station. After an early termination the destination is the termination station, which still lies on the right route.
- **The platform display is built from the board plus `GET /journeys/{id}` of the next train** (for "Calling at"): the contract's `/stations/{id}/platforms/{p}/next` does not exist yet. The calling points are only used while they belong to the train that is next now.

- **Time comes from the server, not from the device (review 19 Oct).** `createServerClock` (`src/services.tsx`) reads the `Date` header of every API answer and of the stream response and keeps the difference to the device clock; between answers the device clock ticks. The header clock and removing trains by the clock (offline) use this server time (`useNow`). The feed's contact timing ("no contact for 2 minutes") only measures elapsed time, so it stays on the device clock. The `Date` header counts whole seconds, so the clock can be up to 1 s behind.
- **A journey is shown once** (`onePerJourney` in `src/rows.ts`): the same `journeyId` twice is dropped, so "Then" never repeats the next train. Seen on the real backend: the board also returns TOMORROW's journeys (same service numbers, other journey ids, rows carry no date), so the board showed every train twice. That is the backend's board query (service dates yesterday to tomorrow); the display cannot tell the days apart and does not guess. Reported to Senuja; the backend should limit the board to the current service day, or add the service date to the row.

### Layout (rehearsal fix #7, 19 Oct)
- **The picture is always 1920 x 1080 and is scaled to the window** (`components/Stage.tsx`, `fitScale`), centred. A smaller window shows the same layout, smaller, so nothing wraps differently and a platform number can never be pushed off the screen. A bigger screen fills up.
- **Board panels are exactly half each** (`grid-template-columns: minmax(0, 1fr) minmax(0, 1fr)`); the direction heading is one line. **Rows have one fixed height (126 px)**: Time (260 px) and Platform (128 px) have fixed widths, the destination takes the rest and is cut with an ellipsis (the train type stays).
- **"Expected hh:mm" sits under the scheduled time, not beside the status badge.** Measured: the badge "Held · 3 min late" (about 420 px) plus "Expected 14:50" (about 250 px) need more than the 530 px a row has left, so they cannot share a line without cutting the status text. The status line now holds only the badge. Senuja's instruction (c) said both on one line; this is the deviation, and the reason. Easy to change back (`BoardLine` in `BoardPage.tsx`) if a cut status text is acceptable to him.
- **A changed platform shows the number with the changed icon and "(was 2)" under it** in the platform column; the full words "Platform 5 (was 2)" (S-10) are its label. "(was 2)" is cut from the fixture text (`platformCell.wasText`), not typed again. The full-width badge no longer sits in the status line.
- Screenshots (mock API with a late, held, cancelled, platform-changed, does-not-stop and long-name row), in `screenshots/`: `board-1920x1080.png`, `board-1366x768.png`, `board-900x700.png` and the same three for `platform-...`. The sizes are the browser's inner size; the headless browser adds some black margin.

### Wording (`src/wording.ts`)
- **All status wording comes from `shared/status-fixtures.json`, and `wording.test.ts` runs every fixture.** Fixtures for the passenger app only (freshness, "offline" of a phone, "does not run today": S-14 to S-17, S-24 to S-28) are listed by id in the test; a fixture shape the test does not know fails.
- **S-19 and S-20 sit where the design puts them: under "Train 1520 to Kandy now departs from Platform 5".** Hold set: "The train will wait for passengers at Platform 5" with the "Held · 3 min late" badge. No hold: "Now departing from Platform 5". The promise to wait appears only for a real hold (`hold: true` on the board row). A next train that is held on THIS platform also gets the S-19 line.
- **A train that has not started says "Scheduled 07:12", not "Expected"** (rehearsal #3, `timeLine` in `wording.ts`): its expected time is only its schedule. The board shows no expected time for it. No fixture holds the label; "Scheduled" is the label already used offline.
- **Offline, a line says "Scheduled" and never a status.** No "On time", "Cancelled" or "Late" is shown when nothing live is known (star rule US-P4), times are the scheduled ones, "Expected" is left out, and the moved-away notice is left out (it could be stale).
- **Offline, trains are removed by the clock** as their scheduled time passes (US-D4 AC3), using the display's own clock.

### Look
- **Colours and sizes only from tokens.** `tokens-display.css` is generated (display type sizes, always the dark theme, `--display-*` colours). Status colours are used only at 36 px and above (tokens rule). The warning block (S-18) is dark text on `--color-warning`, with an icon and words.
- **Bright blue (`--display-surface`) is for the ONE most important thing on a screen** (review 19 Oct). Board: only the page header; the direction heading is a band on the dark surface colour with a 6 px accent underline, rows are dark with thin dividers and a subtle alternation, no bright blue. Platform display: dark header with a divider; the next-train panel is the only bright block, with more padding. Column labels stay at 36 px, the smallest display size in the tokens (muted, thin divider below).
- **Status = icon + text + colour.** A test checks every badge has an icon.
- **"Calling at" scrolls slowly when the line is long; with "reduce motion" it wraps instead.**

## Wording that is not a status, for approval (taken from the design images)
"Departures", "Towards <station>", "Time" / "Scheduled" (offline), "Destination", "Platform", "Expected", "Next", "Calling at", "Then", "Live", "Offline", "Connecting", "No more departures today", "No trains on this platform", "Loading", "Train 1520 to Kandy now departs from Platform 5" (US-D2 AC3). The offline banner text is the one in US-D4. Not in the design, mine: "This station is not known.", "The timetable could not be loaded. Please ask staff.", "This page does not exist...", "Does not stop" for a passing train in the "Then" list and the next-train panel (S-12 wording).

## Open points
1. **Offline uses the last loaded rows, not "the cached day timetable"** (US-D4 AC2): `GET /stations/{id}/timetable` is not there yet. After hours offline the board empties as the trains' times pass.
2. **The platform endpoint (`/platforms/{p}/next`) is not built**; see above. If B adds it, `loadPlatform` is the one place to change.
3. **Noto Sans is not bundled** (a font stack, like ops-web), so the display uses the system font when Noto Sans is not installed. The design uses Noto Sans; a font package was not approved.
4. **Position events (every ~10 s) also re-read the board**, because the instruction was "any event". If that is too much load for many displays, `TrainPositionUpdated` can be skipped in `feed/live-feed.ts` (one line).
5. **Direction needs `GET /lines` at start.** If it cannot be loaded the page says so and tries again every 5 s.
6. **Seen with a mock API and headless Chrome at 1920 x 1080** (board, platform display, S-18, S-19); not yet against the real backend and simulator (not running at the time). The screenshots are not in the repository.
7. **The fixture file was brought over from the passenger branch** (commit "fixtures: S-05 hours wording ..."): develop still had the old one.
