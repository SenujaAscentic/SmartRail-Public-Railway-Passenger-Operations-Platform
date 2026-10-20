# SmartRail – Test inventory (read and run, 20 Oct 2026)

**What this is:** an inventory of what is tested today, made by running every suite and reading the specs. **Nothing was fixed.** Run on the developer machine on 20 Oct 2026, 14:25–14:50 (Sri Lanka time), at commit `45bd114` on `feature/ops-web` (clean tree). Sources: [strategy.md](strategy.md) §5, [user-stories.md](../define/user-stories.md), [threat-model.md](../security/threat-model.md) §3 and §6, [domain model](../develop/01-domain-model.md) §10.

---

## 1. Test counts per level, results, durations

| Level (strategy §2) | Where | Tests | Result | Duration (tests / with build) |
|---|---|---|---|---|
| **Unit: domain rules** (`SmartRail.LiveOperations.Domain`, via `OperationalJourney` etc.) | `backend/tests/Unit/LiveOperations/*.cs` (root folder) | 199 | pass | part of the Unit run below |
| Unit: slice logic (LiveOperations) | `…/Unit/LiveOperations/{CrewPositions 18, GenerateJourneys 13, JourneyViews 33, Outbox 9, Stream 114}` | 187 | pass | |
| Unit: Identity, Network seed, Api clock, Simulator, CateringIntegration | `Unit/Identity 37 · Network 46 · Api 19 · Simulator 14 · CateringIntegration 27` | 143 | pass | |
| **Unit total** | `backend/tests/Unit` | **529** | **pass** (exit 0) | 6.7 s / 24 s |
| **Integration** (Testcontainers, real PostgreSQL 17) | `backend/tests/Integration`: Identity 133 · LiveOperations 163 · Network 30 · CateringIntegration 11 · root 163 (permission matrix 151, OpenAPI 8, demo clock 4) | **500** | **pass** (exit 0) | 4 min 50 s / 5 min |
| **Architecture** (module boundaries) | `backend/tests/Architecture` | **4** | pass | 5.5 s / 10 s |
| **Contract** (backend project) | `backend/tests/Contract` | **0** | the project exists but holds **no tests** (`dotnet test` says "No test projects were found") | – |
| Provider service (RailBites, own service) | `provider/RailBites.Tests` (Testcontainers) | **11** | pass | 17.9 s / 23 s |
| **Web component: ops-web** | `web/apps/ops-web/src/**/*.test.ts(x)` (Vitest + Testing Library, 18 files) | **207** | pass | 77 s (one earlier run 144 s) |
| **Web component: station-display** | `web/apps/station-display/src` (8 files) | **150** | **2 fail (see below)**; the first run passed 150, the next seven runs all failed (2 failures in four runs, 1 in three) | 15 s |
| Web packages | `web/packages/api-client` 5 · `web/packages/tokens` 22 | 27 | pass | a few seconds |
| **Flutter: core package** | `mobile/packages/core/test` | **325** | pass | 54 s |
| **Flutter: passenger app** | `mobile/apps/passenger/test` | **196** | pass | 38 s |
| **End-to-end** (`e2e/`, Playwright) | **the folder does not exist** | **0** | – | – |
| Accessibility (axe), mutation testing, load / rate-limit tests | none | 0 | – | – |

**Total that ran: 1 949 tests** (backend 529 + 500 + 4 + 11 provider = 1 044; web 207 + 150 + 5 + 22 = 384; Flutter 325 + 196 = 521). **2 failing (both in station-display, see below), none skipped, ignored or marked `todo`** (searched C#, TypeScript and Dart for skip and ignore markers: none).

**Contract-type tests (strategy §2 puts these in their own level; in this repo they live inside the levels above):**

| What | Test(s) | Count |
|---|---|---|
| `openapi.yaml` equals what the running app serves | `OpenApiDocumentTests` (integration) + CI step `api:check` (schema.d.ts equals openapi.yaml) | 8 + CI |
| Every public SSE event type has an example, which parses as a public event, shows no staff field | `EventExamplesTests` (unit), `PublicProjectionTests` (12) | 66 + 12 |
| The same examples in the web apps | `ops-web/stream/shared-events.test.ts` (one test per example file), `journey-store.test.ts` | 43 |
| The same examples in Flutter | `core/test/stream/live_message_test.dart`, `sse_parser_test.dart` | 24 |
| `status-fixtures.json` formatted identically in every app | ops-web `status-text.test.ts` (30), station-display `wording.test.ts` (28), Flutter `status_texts_test.dart` + `status_texts_extra_test.dart` (a loop makes one test per fixture case) | all three |
| Backend status text | none (the backend sends kind and minutes, never text) | n/a |
| Webhook signature contract | `RailBitesTests` (5 signature / replay / duplicate / 400 tests) and `CateringRelayTests` (SmartRail's own signing accepted by RailBites) | 6 |

**Station-display failures (found while running, not a code change since the last green run):** `BoardPage.test.tsx › what a line says › shows at most 10 departures` fails every time it is run alone (`no listitem found`), and `offline (US-D4) › after 2 minutes without any contact…` fails in some runs (`expected <span> to be null`). Run 1 of the day: 150 pass. Runs 2 to 8: failures every time (2, 2, 2, 2, then 1, 1, 1 when run alone or with `TZ` set). The tests use fake timers with `shouldAdvanceTime` and wait with `settle(50)`; the machine was busy (many `dotnet` and `dart` processes of other tools). **Treat as flaky under load (S2: CI risk), cause not yet found.**

---

## 2. The five E2E flows (strategy §5)

`e2e/` does not exist and there is no Playwright in the repository or in CI (`.github/workflows/ci.yml` runs backend build + test, web install, `api:check`, typecheck, test and build; no Flutter job, no e2e job, no scans). So **none of the five flows exists as written (Playwright + Flutter integration + recorded manual run).** What exists is API-level coverage of the same chain, in the Integration project.

| # | Flow (strategy text) | Exists in `e2e/`? | Closest automated coverage today (all pass) | What it asserts | Does the flow still match the MVP? |
|---|---|---|---|---|---|
| 1 | Search → live delay | **No** | `JourneyReadTests` (search, detail), `SimulatorEndToEndTests.A_run_with_a_halt_between_stations…` (a `DelayChanged` after a halt), `TrackingRefreshTests` (R-06 / R-23 by the real 15 s timer, incl. Lost + overdue 22 min), Flutter `status_texts_test` (fixture "6 min late") | Search returns the journey; delay appears as an event; the timer makes lateness grow in whole minutes | Mostly. "Within 1 minute of the missed time (SM-2)" is only shown with a fake clock (15 s timer interval), never measured on a real clock across the real apps |
| 2 | Driver starts → train moves in app and on platform display | **No** | `SimulatorEndToEndTests.A_normal_run_…` (start, ≥ 100 accepted fixes, every stop Arrived / Departed, journey Completed, last event < 5 s), `CrewStartEndTests`, `CrewPositionsTests`, station-display `PlatformPage` / `BoardPage` tests on feed events | `JourneyStarted`, positions accepted, stop statuses change, stream delivers in < 5 s | **Text is out of date:** the "driver" is the simulator acting as the cab tablet (driver app deferred, 19 Oct); the platform display shows the train from the public API / stream, not from a driver tap |
| 3 | Controller cancels → push + display | **No** | `CancelDecisionTests` (cancel, reinstate, stream announces it, audit entry), `StreamLatencyTests.A_saved_change_reaches_a_connected_stream_client_in_under_5_seconds`, `ControllerDecisionTests.Every_decision_writes_an_audit_entry…`, ops-web `JourneyPage` cancel dialog test, station-display "Cancelled" row tests | Impact confirmation, `JourneyCancelled` on the stream < 5 s, audit with actor and reason code | **Push is deferred** (P-7, P-5, P-6). The "push sent once to the saved-trip holder" assertion cannot be tested |
| 4 | Platform change < 5 min with hold | **No** | `PlatformDecisionTests` (preview levels, conflicts, hold with the change, < 5 min not refused), `PlatformChangeTests` (unit, R-11 to R-13, `Train_will_wait_message_only_when_hold_is_set`), ops-web `JourneyPage` platform dialog tests, station-display `PlatformPage` tests (S-19 / S-20 wording), `SimulatorEndToEndTests` (hold obeyed) | Preview shows the Late level, hold set, the info screen says "The train will wait" only with a real hold | **Crew instruction is deferred** (US-C6). The simulator now obeys holds and prints "Hold received / released / Platform changed"; the hold is tested, the **"Platform changed" log line is not** |
| 5 | Train delayed → provider gets the new ETA | **No** (no Playwright page test of RailBites' event log) | `CateringRelayTests` (5): a delay reaches RailBites as a signed ETA per catering station, a repeated delay sends nothing, a cancel reaches it with the reason, provider down then back = delivered once, 24 hours of retries then Failed; `RailBitesTests` (signature, tamper, replay, duplicate); `CateringEndpointTests` (menu, provider down) | Signed webhook accepted, retry, no duplicate, train information unaffected while the provider is down | Matches, with two changes: the provider is shown as **RailCantima (code name RailBites)** on its event page; the < 10 s (SM-5) is not measured (fake clock); the flow starts "delay 1001 FROM THE OPS WEB" for the demo |

### Proposed updated flow text (for approval; strategy.md is not edited)

| # | Proposed flow | Automated by | Key assertions |
|---|---|---|---|
| 1 | **Search → live delay:** search the journey; the simulator (as the cab tablet) drives it late; the passenger app and the board show the delay | API integration (existing) + Flutter widget tests with the shared fixtures + one recorded manual run | Search returns the journey; `DelayChanged`; "N min late" exactly as the fixture; growing lateness within 1 min of a missed time, measured on the demo clock |
| 2 | **Simulator starts a journey → the train moves** on the line view, the board and the platform display | `SimulatorEndToEndTests` + display tests + manual run | `JourneyStarted`; positions accepted; stop status changes; platform display shows the approaching train |
| 3 | **Controller cancels (ops web) → displays and app** | `CancelDecisionTests` + ops-web dialog test + display tests | Impact shown before confirming; "Cancelled" on the board and in the app < 5 s (SM-3); audit entry with actor. *Push is deferred: the app learns of it through the live stream (ADR-04 fallback)* |
| 4 | **Platform change < 5 min with hold** | `PlatformDecisionTests` + ops-web platform dialog + display tests + simulator log | Preview shows Late and conflicts; hold set; the platform display says "The train will wait for passengers" only with the hold; **the simulator log shows "Hold received… / Platform changed… / Hold released…" and the train waits until released** |
| 5 | **Train delayed → RailCantima (RailBites) gets the new ETA** | `CateringRelayTests` + a manual look at `http://localhost:5100/events` | Signed webhook for each catering station; provider down then back = delivered once; a cancelled train arrives as `provider.journey-changed` |

---

## 3. ★ acceptance criteria → tests

The user stories mark seven criteria with ★ (moment of truth).

| ★ criterion | Covered by | Status |
|---|---|---|
| **US-P4.1** lateness shown as exact minutes ("On time", "3 min late", "2 min early"); a train that misses its scheduled time shows growing lateness within 1 minute (SM-2); no "Delay likely" labels | Rule: `OverdueDelayTests` (15, incl. the Lost / Stale case), `Delay*` tests (R-01..R-03), `TrackingRefreshTests` (15 s timer, Lost + overdue → 22 min late, public API); wording: `status-fixtures.json` S-01..S-09, S-21..S-27 in ops-web, station-display and Flutter tests; `JourneyViewBuilderTests.A_journey_nobody_has_reported_on_is_NotStarted_never_OnTime` | **Covered** for the rule and the wording. **Not covered:** "no 'Delay likely' labels" (no test), and the timing is never measured end to end. The rehearsal "On time" on 2001 (SM-2) is **unexplained** (open point 56) |
| **US-P7.2** push for a saved trip | none | **Deferred** (P-7, P-5, P-6): not built, no test |
| **US-O3.1** each train shows its position source and age ("GPS · 15 s ago") | Backend: `OpsStreamTests.The_staff_snapshot_says_who_supplied_the_delay_and_each_actual_time…`, `The_staff_stream_adds_source_to_each_event…`; web: `journey-store.test.ts` (remembers the sources), ops-web `JourneyPage` ("where each value came from"), `DashboardPage` ("shows where the delay came from"); Flutter `freshness_test` ("Updated 30 s ago") | **Covered**; no test that checks the exact "GPS · 15 s ago" text on the line view pin / card |
| **US-O4.5** every app, display, crew device and the catering provider show the change within 5 s (SM-3) | `StreamLatencyTests.A_saved_change_reaches_a_connected_stream_client_in_under_5_seconds` (in process, real clock); `SimulatorEndToEndTests` (JourneyEnded < 5 s); `CateringRelayTests` (provider, fake clock) | **Partly:** stream client only. Not measured for the displays, the apps or the provider (SM-5 < 10 s); crew device deferred |
| **US-O5.5** < 5 min: warning before confirming; hold offered; "The train will wait" only with a hold | `PlatformChangeTests` (R-11, `Train_will_wait_message_only_when_hold_is_set`), `PlatformDecisionTests`, ops-web `JourneyPage` ("under 5 minutes: warns before confirming, offers a hold, and promises the train will wait ONLY with the hold"), station-display `PlatformPage` (S-19 / S-20), Flutter `disruptions_test` (no promise without a hold) | **Covered** |
| **US-C3.1** glance view (next stop, scheduled vs expected, delay, dark, landscape) | none | **Deferred** (crew app, 19 Oct): no code, no test |
| **US-F2.1** ETA changes (≥ 1 min), cancellations, early terminations, skipped / added stops, only for catering stations | `ProviderEventRulesTests` (8), `CateringRelayTests` (ETA, repeat, cancel), `RetryScheduleTests` (9) | **Partly:** ETA, cancel and terminate are tested (terminate only in the unit rule test). **Skipped / added stops are not sent** (StopSkipped webhook cut, add-stop deferred). Only catering stations: tested (PLG and PDA only) |

Also checked in this table's scope: **no ★ criterion has a Playwright or manual-run record** (no `docs/qa/scenarios/`, no recorded runs).

---

## 4. Threat model §6 controls → tests

| §6 item | Control | Test that proves it | Gap |
|---|---|---|---|
| **1 Permission matrix** (T-04, T-13) | every endpoint × role | `PermissionMatrixTests`: 151 rows, **29 of 29** operations in `openapi.yaml`, roles Anonymous, Controller, StationManager, Driver (phone), DriverCab; `PolicyTests` | Scope checks are in other tests (StationManager own station: `OpsStreamTests.A_station_manager_gets_only_the_events_of_their_own_station…`; driver not assigned: `CrewStartEndTests.A_driver_cannot_start_a_journey_that_is_not_assigned…`, `CrewPositionsR07Tests`). **Display read-only: no test** (no display credential; displays use the public GET API, T-13 status note) |
| **2 Authentication** (T-01, T-14) | lockout, generic errors, expiry, refresh reuse | Lockout: `StaffSignInTests.Five_wrong_passwords_lock_the_account_and_the_right_password_is_then_refused_too`, `CrewSignInTests.Five_wrong_passwords_lock_the_driver_account`, `CabSignInTests.Five_wrong_driver_passwords_on_a_valid_tablet_lock_the_account` and `A_bad_device_credential_never_counts_against_the_drivers_lockout`; generic: `CabSignInTests.*generic_401`, `StaffSignInTests.A_driver_cannot_sign_in_here…same_generic_answer`, ops-web `SignInPage` tests; expiry: `OpsStreamTests.The_staff_stream_ends_when_the_access_token_expires`, `DemoClockStartupTests`; reuse: `RefreshSessionTests.A_third_use_of_the_same_token_ends_the_whole_session…`, `RefreshTokensTests`; cookie flags: `StaffSignInTests.The_refresh_token_travels_only_in_an_HttpOnly_Secure_SameSite_Strict_cookie`; timing: `SignInTimingTests` | The 30-minute idle expiry is tested only in the ops-web `session.test.ts` (client side). Audit actor on every decision: `ControllerDecisionTests.Every_decision_writes_an_audit_entry…` |
| **3 Integration** (T-03) | tampered signature, old timestamp, duplicate | `RailBitesTests`: wrong / missing signature, body changed after signing, > 5 min old or from the future, duplicate incl. concurrent, 400 cases; `CateringRelayTests` (SmartRail's real signature accepted) | Good. No shared JSON example of the webhook body (see §6 gaps) |
| **4 Input** (T-08, T-09) | injection strings, oversize | `<script>` as literal text: `RailBitesTests.The_event_page_lists_received_events_and_shows_a_reason_only_as_text`, ops-web `LivePage` (`<b>` in a note is text), Flutter `disruptions_test` (note is plain text); length: `SearchStationsTests.A_query_longer_than_50_characters_is_refused`, `SearchJourneysRequestTests`, `StationBoardEndpointTests` | **No test with SQL-injection strings** (only EF parameterisation by design and review). No oversize-body test for the crew positions batch. Announcements (T-08's main case) are deferred |
| **5 Data exposure** (T-06, T-07) | public stream content, log scan | Public stream: `StreamTests.Public_events_never_contain_source_staff_or_session_fields`, `PublicProjectionTests` (12), `EventExamplesTests.Examples_show_no_staff_fields`, `JourneyReadTests.The_detail_shows_no_source_and_no_staff_fields`, `JourneyViewBuilderTests.The_public_json_never_contains_source_or_staff_fields`; logs: `CabSignInTests.Neither_the_device_secret_nor_the_password_is_ever_written_to_a_log`, `OutboxDispatcherTests.The_dispatcher_logs_ids_and_types_only`, `AccessTokenIssuerTests.The_token_holds_no_personal_data…`, ops-web `stream-client.test.ts` (token in a header, never the URL) | **No scan of all log lines for emails and phones** (T-06's stated evidence); the relay and menu-pull logs have no log test |
| **6 Abuse** (T-10) | rate limits, connection limits | none | **Not built** (the threat model says "planned 17 Oct"); sign-in has only the account lockout. No test |
| **7 Pipeline** (T-15, T-16) | secret scan, dependency scan | none | **Not in CI** (no secret scan, no `dotnet list package --vulnerable`, no `npm audit`, no Dependabot config) |
| **8 AI review** | independent agent reviews auth and webhook code | `.claude/agents/spec-reviewer.md` exists | No recorded findings (`docs/qa/defect-log.md` does not exist) |

Other §3 rows with a stated test: T-02 fake positions (`CrewPositionsR07Tests`, `PositionRulesTests`, discarded-fix tests: **covered**); T-05 IDOR (no passenger accounts: n/a); T-11 audit append-only at database level (**no test**; the audit table's rights are not tested); T-12 stolen tablet (`CabSignInTests.A_deactivated_device_is_the_generic_401…`, secret never logged: **covered**); T-17 wrong tablet clock (`ClockOffsetTests`, `CabSignInTests` 24 h rule, `CrewPositionsTests`: **covered**); T-18 repeated crew action (`CrewPositionsTests.Sending_a_batch_again_with_the_same_clientActionId…`, start / end: **covered**); **T-19 over-posting (a request that sets `"source": "Controller"`): no test**; T-20 provider down (`CateringRelayTests`, `CateringEndpointTests`: **covered**); T-08 and T-14 CSP: **no test**.

---

## 5. Approved test names (domain model §10) → present / missing

The list in §10 has **78 names** (strategy.md still says 44).

| Result | Count |
|---|---|
| Present with the exact name | **68** |
| Present under another name | **4** |
| Missing | **6** (all belong to deferred features) |

Present under another name:

| Approved name | Test that covers it |
|---|---|
| `New_cab_sign_in_ends_previous_cab_session` | `CabSignInTests.Signing_in_on_another_tablet_ends_the_drivers_first_session_completely`, `A_second_driver_signing_in_on_the_same_tablet_ends_the_first_drivers_session` |
| `Start_journey_refused_in_phone_mode` | `CrewStartEndTests.A_phone_token_cannot_start_a_journey_even_for_the_assigned_driver` |
| `Driver_cannot_act_on_journey_not_assigned_to_them` | `CrewStartEndTests.A_driver_cannot_start_a_journey_that_is_not_assigned_to_them_and_nothing_changes` (positions: `CrewPositionsR07Tests`) |
| `Queued_crew_tap_does_not_overwrite_newer_controller_value` | the rule half: `ControlVersionTests.A_controller_delay_is_kept_when_a_crew_tap_or_gps_arrives_later`, `OperationalJourneyLiveTests.Controller_delay_is_kept_when_gps_and_crew_report…`; the offline queue itself is the deferred crew app |

Missing:

| Approved name | Why |
|---|---|
| `Unscheduled_stop_alert_after_3_minutes_stopped_outside_any_zone`, `No_unscheduled_stop_alert_when_stopped_inside_a_station_zone`, `Only_one_unscheduled_stop_alert_per_stop` | O-8 unscheduled stop alert deferred 12 Oct (no code) |
| `Station_manager_cannot_post_announcement_for_another_station` | O-9 announcements deferred 19 Oct (no code); the scope idea is tested for the staff stream |
| `Push_sent_when_delay_changes_by_5_minutes`, `Push_not_sent_twice_for_the_same_change` | P-7 push deferred 19 Oct (no code) |

Names added since §10 was approved and not in it (tests exist): the whole R-23 Lost case, the skipped-stop GPS time (`SkipStopTests`), `HoldGateTests`, `RetryScheduleTests`, the catering and RailBites tests.

---

## 6. Gaps, prioritised

**P1: fix or decide before the demo**

1. **No E2E at all.** `e2e/` and Playwright do not exist, so Q-1 ("5 automated E2E flows") is not met. The flows are covered only at API level, with fake clocks. Decide: build a thin Playwright run for flows 1, 3 and 5 (web only), or record a manual run and change Q-1's wording.
2. **station-display tests are flaky under load** (2 failing now; CI risk). Find the race (`settle(50)` plus fake timers) before CI runs on `develop`.
3. **SM-2 rehearsal "On time" (R-23) is unexplained.** The rule and the timer are proven by tests while the train is Lost. Candidates (a controller delay of 0 blocks R-23; a restart rewinds the demo clock; one failing journey stops the whole refresh round) have **no test**: add a test that one journey whose refresh throws does not stop the others, and make the cause visible in the ops web (delay source is shown there).
4. **★ US-F2.1 is partly unmet:** skipped / added stop events are not sent to the provider. Either build `StopSkipped` → `provider.journey-changed`, or mark the criterion "ETA, cancel, terminate" in the story.
5. **★ US-P7.2 and US-C3.1 are deferred but still marked ★** in the stories; mark them deferred so the ★ count is honest. **★ US-O4.5** is measured only for the stream client; nothing measures displays, apps or the provider (SM-3, SM-5).

**P2: security evidence the threat model promises**

6. No test for **T-19 over-posting** (`source` in a crew body), **T-11** audit append-only rights, **T-08 / T-14 CSP**, **T-06 whole-log scan** for emails / phones, SQL-injection strings.
7. **T-10 rate limits and SSE connection limits are not built** (no test either); **T-15 / T-16** secret and dependency scans are not in CI.
8. **Display read-only (T-13) cannot be tested** (credential deferred); the public API is the only displays path (documented in the threat model).

**P3: process and documents**

9. **CI runs no Flutter tests** (521 tests only run locally), no e2e, no accessibility check. The `Contract` test project is empty (builds, runs nothing); either delete it from the solution or use it.
10. **Contract tests do not cover everything the rules ask for:** the provider webhook bodies (`provider.eta-updated`, `provider.journey-changed`) have no shared example file or parse test (only the end-to-end relay test); Flutter's fixtures under `core/test/fixtures` are copies, not parsed from `openapi.yaml` (CLAUDE.md §5.4); the catering endpoint example in contract §3.6 is not parsed by a test.
11. **Docs behind the code:** strategy.md says 44 names (78 now) and still describes push and the crew app in flows 3 and 4; §10 lists six names for deferred features without saying so; `docs/qa/scenarios/` and `docs/qa/defect-log.md` (strategy §6, §8) do not exist; the simulator's "Platform changed…" log line has no test.
