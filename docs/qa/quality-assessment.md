# SmartRail – Quality assessment (21 Oct 2026)

Facts only. Sources: [test-inventory.md](test-inventory.md) (20 Oct), [strategy.md](strategy.md) §5 (E2E run of 21 Oct, `docs/qa/evidence/`), the commits named below. **`docs/qa/defect-log.md` does not exist** (strategy §8 promises it); the defect table below is rebuilt from commits, so the severities are mine, using the scale in strategy §8.

## 1. Success measures (moscow.md)

| Measure | Target | Result | Evidence |
|---|---|---|---|
| **SM-1** ETA accuracy | 80% within ±2 min | **Not measured.** No run compares predicted with actual arrivals over a journey, and the simulator drives one train at a time. The rules behind the ETA (R-01 to R-03, R-23) are unit-tested | – |
| **SM-2** no false "On time"; time from falling behind to the app showing it | < 1 min | **Partly.** A delay is announced in the same save as the departure that shows it and reaches the ops web in **1.8 s** and a station board in **2.3 s** (flow 1). Growing lateness of a train that misses a stop is tested with a fake clock only (the refresh runs every 15 s). The **passenger app is not driven** (Flutter fixture tests only). The rehearsal "On time" on 2001 is **unexplained** | flow1.json, `TrackingRefreshTests`, `OverdueDelayTests` |
| **SM-3** controller action → everything updated | < 5 s | **Met where measured:** cancel to board **0.8 s** (flow 3); platform change to the new and old platform displays **1.0 s** and **1.0 s** (flow 4); delay to ops web 1.8 s and board 2.3 s (flow 1). Not measured: the passenger app and the crew device (deferred) | flow1, flow3, flow4 .json, `StreamLatencyTests` |
| **SM-4** share of platform changes with < 5 min notice | measured and reported | **Not measured.** The lead time level is stored and announced per change (R-11), but there is no report or count; the one E2E change had a 3 min lead time. A real share needs real use | flow4.json |
| **SM-5** events to the provider | 100% with retries; < 10 s when up | **Met in the tests:** delay to provider **2.1 s**; after a provider outage the ETA arrived 3.4 s after it was back, once, no duplicate; retries run 24 h on a fake clock (`CateringRelayTests`, `RetryScheduleTests`). Skipped and added stops are not sent (open) | flow5.json |

All E2E times are real wall-clock milliseconds on one machine (the demo clock ran 5× faster), so they are lower than over a network.

## 2. Tests

| Level | Tests | Result (last run) |
|---|---|---|
| Unit (domain rules and slice logic) | 529 | pass |
| Integration (Testcontainers, PostgreSQL) | 520 (incl. 19 security tests, the permission matrix 151 rows) | pass |
| Architecture | 4 | pass |
| Provider service (RailBites) | 11 | pass |
| Web: ops-web 207, station-display 152, packages 27 | 386 | pass (station-display flaked under load on 20 Oct, see §4) |
| Flutter: core 325, passenger 196 | 521 | pass (run locally) |
| **E2E (Playwright, 5 flows)** | 5 | pass, 12.5 min |
| **Total** | **1 976** | none skipped or ignored |

No `Contract` project (removed, empty); contract tests live in each app (openapi, 66 event-example tests, status fixtures in three apps). No accessibility (axe), mutation or load tests. Approved test names (domain model §10): 72 of 78 present; the 6 missing belong to deferred features (unscheduled-stop alerts, announcements, push).

## 3. ★ criteria (7 in user-stories.md)

Covered: **US-P4.1** (rule, wording, timer), **US-O3.1** (source and age), **US-O5.5** (under-5-minute warning, hold, wording; also flow 4). Partly: **US-O4.5** (measured on the stream and two displays, not the apps), **US-F2.1** (ETA, cancel, terminate; skipped and added stops are not sent). Deferred, still marked ★: **US-P7.2** (push), **US-C3.1** (glance view): no code, no test.

## 4. Defects (by severity, strategy §8)

| Sev. | Defect | Status |
|---|---|---|
| **S1** | One journey whose tracking refresh threw stopped the refresh of **every** train (no delay growth, a possible false "On time"); found by a test, `688c66b` | **Fixed**, test first |
| **S1** | The station board said "Does not stop" for a **cancelled** train (every stop of a cancelled journey is Skipped); found by E2E flow 3, **`60032a6`** | **Fixed**, test first (also "Terminates at" for early-ended trains) |
| S1 | Rehearsal "On time" for 2001 with tracking Lost (SM-2) | **Open, cause not proven.** R-23 itself holds while Lost (tests); the refresh-isolation defect above is the best candidate |
| S2 | Native selects in the decision dialogs closed by themselves on live trains (focus stolen on every stream event), `51946fa` | Fixed |
| S2 | CI red: ops-web status wording differed from `status-fixtures.json`, `b8b76dd` | Fixed |
| S2 | station-display tests `shows at most 10 departures` and `offline after 2 minutes` failed in seven runs on a busy machine; passed in the later full run (152) | **Open**, flaky under load, cause not found |
| S3 | US-F2.1: skipped and added stops are not sent to the provider | Open (StopSkipped webhook cut; add-stop deferred) |
| S3 | A skipped stop the train runs through gets its pass time (`5872d3d`), but the passenger app does not count it in "Last passed" and no event announces it | Open (code guide point 55; app change or event) |
| S4 | CLAUDE.md still lists a `Contract` test folder; strategy and delivery plan text were behind (fixed in strategy) | Logged |

## 5. Security controls and their tests

| Control | Test |
|---|---|
| Permission matrix (T-04) | `PermissionMatrixTests`: 29 of 29 operations × 5 roles |
| Lockout, generic errors, refresh reuse, cookie flags (T-01, T-14) | staff, crew and cab sign-in tests, `RefreshSessionTests` |
| HMAC, replay window, duplicates (T-03) | `RailBitesTests` (5), `CateringRelayTests` |
| Over-posting `source` ignored (T-19) | `SecurityTests` (new) |
| Injection strings in notes and search; SQL in a reason code is a 400 (T-08, T-09) | `SecurityTests` (new) |
| No secrets, tokens, e-mail or phone in any log line (T-06) | `SecurityTests` log scan (new) and specific log tests |
| Public stream shows no staff data (T-07) | `StreamTests`, `PublicProjectionTests`, event examples |
| Provider down does not block SmartRail (T-20) | `CateringRelayTests`, `CateringEndpointTests`, refresh isolation |
| Vulnerable packages (T-16) | CI steps added (`b84e712`): clean today; the failing path was **not** exercised (no real advisory) |

## 6. Accepted risks

No rate limits or stream connection limits (T-10); no Content-Security-Policy (T-08, T-14); audit log append-only by convention, not by database rights (T-11) (all three written in threat-model.md §5); **no display tokens**, displays use the public read-only API (T-13, deferred); **E2E not in CI** (needs Docker, .NET, Node, Chromium); **Flutter tests not in CI** (521 tests run locally only); no secret scan in CI; no staff 2FA, no MDM, single instance.

## 7. What I would test next

1. SM-1: a run that compares the ETA at each stop with the actual arrival over a whole simulated journey. SM-4: a count of platform changes by lead time level.
2. The passenger app against the live stack (Flutter integration or Patrol), for SM-2 and SM-3 on the app side.
3. R-23 at real time (a train that stops between stations), and the 2001 case once its cause is known.
4. E2E and Flutter jobs in CI; a deliberate failing check of the vulnerability grep.
5. Rate limits, a CSP header test, audit-table rights, display-token rows in the permission matrix (when the credential exists).
6. A shared example and parse test for the provider webhook bodies; the catering endpoint example.
7. axe scans on the ops web and the displays; mutation testing on the domain core; the station-display flake.
