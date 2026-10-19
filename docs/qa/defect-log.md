# SmartRail – Defect log

**Started:** 19 Oct 2026 (rehearsal of the 20 Oct demo) · **Owner:** Senuja · **Format:** [strategy.md, section 8](strategy.md)

Severity here is High / Medium / Low. Against the strategy's scale: High = S1 or S2 (wrong or hidden safety-relevant information, or a flow that cannot be shown), Medium = S2 with a workaround, Low = S3 or S4.

Status: **Fixed** (with the commit), **In progress** (someone is on it, not merged), **Deferred**, **Known limitation** (accepted, will not be fixed for the demo). The commit hashes are on the `fix/rehearsal` branch or the branch named; the numbers #1 to #8 are the rehearsal's own numbering.

## Rehearsal, 19 Oct 2026

| ID | Found | Description | Severity | Status | Verified by |
|---|---|---|---|---|---|
| #1 | 19 Oct, rehearsal (starting the demo by hand) | `demo-start.ps1` started the web apps without building the design tokens (they are generated, not committed), so the apps could not load their generated styles | Medium | Fixed, `8e61a37` | Retest: ran `demo-start.ps1` on a scratch API (port 5046); the script prints "Wrote ... tokens.css" before the web apps start. No automated test (a script) |
| #2 | 19 Oct, rehearsal | `npm ci` run while old dev servers were still running failed on locked files in `node_modules` | Medium | Fixed, `8e61a37` | Retest: with a node process started from the repository running, `demo-start.ps1 -ReinstallWeb` listed it and exited; the process was left alone. No automated test |
| #3 | 19 Oct, rehearsal (display and passenger app) | A train that has not started was labelled "Expected" on the platform display and in the app, but nothing live is known about it: its expected time is only its schedule | Medium | Fixed, `85a035f` | Tests: `PlatformPage.test.tsx` "a train that has not started says 'Scheduled', not 'Expected'", `BoardPage.test.tsx` "a train that has not started", `not_started_test.dart` (details column "Sched.", waiting screen "Scheduled at Gampaha"). Wording "Scheduled at <station>" approved 19 Oct |
| #4 | 19 Oct, rehearsal (second simulator) | Starting the simulator for 2001 with DRV-02 was refused with `403 OUT_OF_SCOPE`. The backend was right: the seed gives 2001 the train set LH-01, so its driver is DRV-03 and its cab CAB-02. The runbook (and the demo setup) named the wrong cab | Low | Fixed, `8e61a37` (seed unchanged: it was correct) | Retest: `demo-start.ps1 -WithSlowTrain` on port 5046 started both simulators; 2001 became Live with DRV-03 / CAB-02. Runbook corrected and a service / driver / cab table added (code guide 4.15) |
| #5a | 19 Oct, rehearsal | The simulator ignores holds: it does not react to a hold the controller has set | Medium | In progress (B, backend / simulator); not merged at the time of writing | Not yet verified. Retest after B's merge: set a hold in the ops web and watch the simulated train |
| #6 | 19 Oct, rehearsal (ops web) | Drop-down lists in the controller's decision dialogs closed, and typed input was lost, whenever a live update arrived | High (a controller cannot finish a decision while trains are live) | Fixed, `51946fa` (B) | `JourneyPage.test.tsx` and the stream helper `test/sse.ts` were changed in the commit; retest by Senuja in the ops web with the simulator running. The commit also makes a stale decision say "This train changed" on a 409 |
| #7 | 19 Oct, rehearsal (departures board) | A delayed row (badge and "Expected 07:09") was taller, so the two direction panels no longer lined up; in a smaller window the left panel grew, "Towards Colombo Fort" wrapped and the platform "1" was pushed off the screen | High (a platform number must never be hidden) | Fixed, `47839b9` | `Stage.test.ts` (scale to any window), `BoardPage.test.tsx` (delayed row, changed platform "(was 2)"); screenshots at 1920x1080, 1366x768 and 900x700 in `web/apps/station-display/screenshots/`. "Expected" now sits under the scheduled time (approved) |
| #8 | 19 Oct, rehearsal (hold on 2001 at Polgahawela) | With a hold set, the app showed "On time" and "Reason: Congestion" and nothing about the hold. The backend sends a hold as a flag and keeps the status kind | High (a status that hides a hold is misleading) | Fixed, `82ce634` | Tests: `hold_test.dart` (details, tracking, search card: Held badge while the hold is set, gone when released, cancelled stays Cancelled), `status_with_hold_test.dart`, `BoardPage.test.tsx` and `PlatformPage.test.tsx` "a hold (rehearsal #8)". Wording is S-21 "Held" / S-08 "Held · N min late", no new text. Limit: a hold at a station ahead is not on a board row |
| #9 | 19 Oct, CI (automatic): the build was red | Fixture drift: the ops web's status wording no longer matched `shared/status-fixtures.json` (S-05 hours, S-21 Held, S-22 calendar, S-23, S-25 to S-27) and its fixture test failed | Medium (status wording must come only from the fixtures) | Fixed, `b8b76dd` (B) | `status-text.test.ts` runs every fixture; CI green again |
| #10 | 19 Oct, rehearsal | "Last passed" shows the wrong station when the train ran through a stop that is skipped | Low | In progress (B), otherwise a known limitation for the demo | Not yet verified. Retest after B's change: skip a stop the train runs through and read "Last passed" |
| #11 | 19 Oct, review of the catering relay against the contract | Catering webhook retries stop after 15 minutes (6 attempts, then Failed) but the contract (3.6) says 10 s, 30 s, 1 min, 5 min, then every 15 min for 24 h | Medium (an event can be lost after a long provider outage; SM-5 asks for 100% delivery) | In progress (B) | Not yet verified. `CateringRelayTests` tests the current schedule; it must change with the fix, and a test must show delivery after more than 15 minutes of outage |

## Process findings

| ID | Found | Finding | Severity | Status |
|---|---|---|---|---|
| P-1 | 19 Oct (fixture drift, #9) | **CI was red for 8 hours and merges continued.** A red build is only useful if it stops the line: further merges on top of it hide new failures and make the cause harder to find | High (process) | **Process fix: no merge into `develop` while CI is red.** A red build is fixed first, or the change that broke it is reverted |

## Notes

- Defects #1, #2 and #4 are in the demo script; the script has no automated test, so they were checked by running it on a scratch setup (own API port and database, never the demo backend).
- The README still says `demo-start.ps1` does not start RailBites. B's change to the script is not on `develop` yet; the line is updated when it is.
