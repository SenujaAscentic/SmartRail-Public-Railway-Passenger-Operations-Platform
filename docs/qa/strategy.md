# SmartRail – QA Strategy v1

**Phase:** Develop · **Version:** v1 draft, 9 Oct 2026 (completed with evidence 17–18 Oct) · **Owner:** Senuja
**Brief:** §18 asks which tests give the most value and why; §23 asks for a strategy covering the full spectrum of quality, functional scenarios, 5 automated end-to-end flows, defects, evidence and a quality assessment.
**Inputs:** quality goals (architecture Q1), success measures SM-1–SM-5, ★ acceptance criteria, edge cases E-01–E-46, rules R-01–R-22, threat model v1, ADR-08.

---

## 1. What "quality" means for SmartRail

| Quality goal | What would count as failure | How we test it |
|---|---|---|
| **1. Real-time speed** | A controller action reaches a screen after 5 s (SM-3); a delay shows after 1 min (SM-2) | E2E flows measure action → screen time; integration tests on stream latency |
| **2. Reliability** | A lost or wrong event; apps disagree; "On time" for a late train | Unit tests on rules (TDD); outbox and retry tests; shared status fixtures in every app; resilience scenarios |
| **3. Security** | Anyone acts beyond their role; data leaks | Permission matrix, threat model tests (threat-model.md §6) |
| Accessibility | Status only by colour; unreadable boards; unusable with a screen reader | axe scans, manual screen-reader check, contrast tokens |
| Usability under stress | Passenger misreads a board; driver distracted | ★ criteria, glance-view rules, design review against personas |

**Approach in one sentence:** test hardest where a bug would mislead passengers or staff (rules, permissions, event delivery, cross-app consistency), and lightest where a bug is visible and cheap to fix (layout).

---

## 2. Test levels

| Level | Tool | Covers | Written | Share of effort |
|---|---|---|---|---|
| **Unit: domain rules** | xUnit + `FakeTimeProvider` | R-01–R-22 (delay, ETA, detection, freshness, lead time, conflicts, priority, scope) | **Test-first** (78 approved names, domain model §10; the list grew from 44 as rules were added on 15 to 18 Oct) | High |
| **Integration: API + database** | xUnit + Testcontainers (real PostgreSQL) | Endpoints, transactions, concurrency (409), outbox, permission matrix, idempotency | With each endpoint | High |
| **Architecture** | NetArchTest | No module references another module's internals (architecture Q5) | Day 10 | Low |
| **Contract** (no separate project: the tests live in each app, see the note below the table) | xUnit (backend) · Vitest · Flutter test | `openapi.yaml` diff in CI; `shared/events/*.json` and `status-fixtures.json` parsed and formatted identically in all apps; webhook signature contract | Day 11 onward | Medium |
| **Component / widget** | Vitest + React Testing Library · Flutter widget tests | Status rendering, freshness labels, offline banner, pending crew taps, confirmation dialogs | After each screen | Medium |
| **End-to-end** | Playwright (+ Flutter integration tests, see §4) | The 5 critical flows | 17 Oct | Medium |
| **Accessibility** | axe (Playwright) · manual TalkBack check | WCAG 2.2 AA basics; status never by colour alone | 16–17 Oct | Low |
| **Security** | Integration tests + scans | threat-model.md §6 | 16–17 Oct | Medium |
| **Resilience** | Simulator scenarios + manual | GPS loss, tunnel, provider down, display offline, crew offline queue | 16–18 Oct | Medium |

> **Where the contract tests are (20 Oct):** there is no `backend/tests/Contract` project; the empty one was removed from the solution. Each app tests the contract itself: backend `OpenApiDocumentTests` (the served `openapi.yaml` equals the committed file, fields and `$ref`s checked) and `EventExamplesTests` / `PublicProjectionTests` (every `shared/events` example parses as a public event and shows no staff field); web `shared-events.test.ts`, `status-text.test.ts` and `wording.test.ts` (the same examples and `status-fixtures.json`) plus CI `api:check` (`schema.d.ts` equals `openapi.yaml`); Flutter fixture tests under `mobile/packages/core/test`. The provider webhook is covered by `RailBitesTests` and `CateringRelayTests`.

---

## 3. Risk-based focus

| Risk area | Why high risk | Extra measures |
|---|---|---|
| Time-dependent rules | Off-by-one-minute and time-zone bugs are invisible until live; AI-written logic is often subtly wrong here | TDD; controllable clock; boundary cases (59 s, 2:00, 5:00, 15:00 min) |
| Permissions | One missing check = anyone can cancel trains | Automated matrix of every endpoint × role |
| Event delivery | Lost events break "same answer everywhere" | Outbox tests; reconnect and resume tests; provider-down test |
| Cross-app consistency | Three languages format the same status | Shared fixtures run in every app's test suite |
| Offline behaviour | Hard to test by hand, easy to break | Simulator scenarios; widget tests for pending / stale states |
| **AI-generated tests** | An agent can write tests that pass without testing anything | I approve test names before code; review assertions; optional mutation testing (Stryker.NET) on the domain core to prove the tests catch real bugs |

---

## 4. Automating flows that cross native mobile apps (decision K-2)

Playwright drives browsers only. It cannot tap buttons in the native Flutter passenger or crew apps, yet four of the five flows involve them.

| Option | Verdict |
|---|---|
| A. Playwright for everything (build Flutter for web too) | Rejected: tests a different build than the real Android app; Flutter web is hard to automate |
| B. Appium / Patrol driving the Android emulator in the same run | Possible but heavy setup; risky in the time available. Could, for one flow, if time allows |
| **C. Split each flow at the API / event boundary** (proposed) | **Playwright** drives the web apps (ops web, displays) and checks the API state and **the event the mobile app receives**; **Flutter integration tests** prove the app renders that exact event correctly, using the same event fixtures; a **recorded manual run** on the emulator shows the whole flow end to end |

▶ **Decision for you:** option C as proposed?

---

## 5. The 5 end-to-end flows (final text, run 21 Oct 2026)

**How they run:** `e2e/` (Playwright 1.64.0, `npm run e2e`) starts its own stack and removes it at the end: a scratch PostgreSQL in Docker, the API, RailBites (shown as RailCantima), the ops web and the station display, on ports of their own, on a demo clock that runs 5 times faster than real time (so a train moves in minutes; every latency below is real wall-clock time). Playwright drives the ops web and the displays and checks API state and events; the simulator acts as the cab tablet (the driver app is deferred); the passenger app side is covered by the Flutter fixture tests (option C in section 4). Each flow writes its numbers to `docs/qa/evidence/<flow>.json`; traces and screenshots are in `docs/qa/evidence/`. Order of the run: 5, 3, 1, 4, 2.

| # | Flow | Automated by | Key assertions | Measured (21 Oct) |
|---|---|---|---|---|
| 1 | **Search → live delay.** Search finds 2001; the simulator (as the cab tablet) drives it 6 minutes late; the live stream announces the delay; the ops web and the station board show it | `e2e/tests/03-flow1-search-live-delay.spec.ts` + Flutter fixture tests for the app's words | Search lists the train as NotStarted; `DelayChanged` with 6 minutes; API says Late 6; the ops web and the Gampaha board say "6 min late" | SM-2: delay event to ops web **1777 ms**, to the board **2287 ms** (limit 60 s). The delay is announced in the same save as the departure that shows it (0 ms) |
| 2 | **Simulator starts a journey → the train moves** | `05-flow2-train-moves.spec.ts` | `JourneyStarted`; the departure event; at least 3 position updates (25 seen); tracking Live; the train is further along 8 s later (0.19 to 0.32 of the section); no staff field (source, device, session) in any public stream message; the ops web draws the train on the line | start to departure event 49770 ms (includes the wait for the simulator's start window) |
| 3 | **Controller cancels (ops web) → station display** | `02-flow3-cancel-to-display.spec.ts` | The dialog shows what the cancellation affects before anything is sent; "Cancelled" on the Polgahawela board; API status Cancelled, all 11 stops Skipped, version 1; the audit page shows `JourneyCancelled` by CTRL-01 | SM-3: confirm to board **818 ms** (limit 5 s). *Push is deferred: the app learns of it through the same live stream (ADR-04 fallback)* |
| 4 | **Platform change under 5 minutes with a hold** | `04-flow4-platform-change-hold.spec.ts` | At demo clock 06:26 the lead time is 3 min: the dialog says Late, recommends (does not force) a hold; its preview promises "The train will wait" only when the hold is ticked; after confirming, the new platform display says "The train will wait for passengers at Platform N", the old platform display points to the new platform; the platform change and the hold are in the API; releasing the hold removes the promise and the old display says "Now departing from Platform N" (no promise). *The crew instruction is deferred* | SM-3: confirm to new platform display **964 ms**, to old platform display **971 ms** (limit 5 s) |
| 5 | **Train delayed → RailCantima (RailBites) gets the new ETA** | `01-flow5-delay-to-provider.spec.ts` | A delay set in the ops web reaches the provider as a signed webhook, one ETA for each catering station (PLG, PDA) and no other, scheduled time + 15 min; the provider is stopped, the train is delayed again, the provider comes back: each station gets the new ETA exactly once, no event stored twice | SM-5: delay to provider **2066 ms** (limit 10 s); after the outage the retried ETA arrived 3400 ms after the provider was back |

**What these runs do not measure:** SM-2's "growing lateness of a train that misses its stop" (R-23) at real time (covered by the integration test with a fake clock); SM-3 for the passenger app and for the crew device (deferred); everything runs on one machine, so the latencies are lower than over a network.

**Run alone or in order:** the whole run takes about 12 minutes (21 Oct: 5 of 5 passed in 12.5 min). Flow 4 waits for the demo clock to reach 06:26 (its window is 06:26 to 06:27:30, so the lead time is under 5 minutes) and flow 2 waits for 06:25, so run alone each takes about 9 minutes; the file names carry the run order (01 to 05). The run is not in CI yet (it needs Docker, the .NET SDK, Node and Chromium).

---

## 6. Functional test scenarios

- **Source:** every ★ acceptance criterion, plus edge cases E-01–E-46 tagged to the story they belong to.
- **Format:** Given / When / Then, one file per epic in `docs/qa/scenarios/`, each scenario marked *automated (level)* or *manual*.
- **Target:** every ★ criterion automated; every edge case at least listed with how it is covered (or consciously not covered).

---

## 7. Test data and environments

| Item | Approach |
|---|---|
| Network | Seeded, verified, deterministic (US-B1) |
| Time | `FakeTimeProvider` in tests; simulator can run faster than real time |
| Scenarios | Named simulator scenarios: on-time run, delay, stop between stations, GPS loss, tunnel, wrong tablet clock |
| Reset | `POST /dev/reset` (development only) before each E2E run |
| Environments | Local Docker Compose (PostgreSQL ×2, backend, provider); CI on every push |

---

## 8. Entry, exit and defects

- **A story enters testing** when its acceptance criteria and test names are approved.
- **A story is done** per the definition of done in the delivery plan.
- **Feature freeze** at the end of 16 Oct (gate G3); 17–18 Oct are tests and fixes only.
- **Defect log** (`qa/defect-log.md`): ID, found by (test / manual / AI review), steps, expected vs actual, severity, status, fix commit.

| Severity | Meaning | Rule |
|---|---|---|
| S1 Critical | Wrong or missing safety-relevant information; security hole; a flow broken | Fix before demo |
| S2 Major | Feature wrong, workaround exists | Fix if time; otherwise documented |
| S3 Minor | Cosmetic or rare | Logged |
| S4 Trivial | Wording, polish | Logged |

---

## 9. Quality assessment (18 Oct)

A short report against SM-1–SM-5 and the quality goals: measured values from the E2E runs, test counts per level, coverage of ★ criteria and edge cases, open defects by severity, accepted risks, and what I would test next with more time.

---

## 10. Evidence kept for §23

CI run results · test reports per level · E2E traces and screenshots (Playwright) · recorded mobile flow runs · axe reports · security test results and scan reports · defect log · this strategy · the quality assessment.
