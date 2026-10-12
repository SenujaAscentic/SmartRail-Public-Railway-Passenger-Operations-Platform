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
| **Unit: domain rules** | xUnit + `FakeTimeProvider` | R-01–R-22 (delay, ETA, detection, freshness, lead time, conflicts, priority, scope) | **Test-first** (44 approved names, domain model §10) | High |
| **Integration: API + database** | xUnit + Testcontainers (real PostgreSQL) | Endpoints, transactions, concurrency (409), outbox, permission matrix, idempotency | With each endpoint | High |
| **Architecture** | NetArchTest | No module references another module's internals (architecture Q5) | Day 10 | Low |
| **Contract** | xUnit (backend) · Vitest · Flutter test | `openapi.yaml` diff in CI; `shared/events/*.json` and `status-fixtures.json` parsed and formatted identically in all apps; webhook signature contract | Day 11 onward | Medium |
| **Component / widget** | Vitest + React Testing Library · Flutter widget tests | Status rendering, freshness labels, offline banner, pending crew taps, confirmation dialogs | After each screen | Medium |
| **End-to-end** | Playwright (+ Flutter integration tests, see §4) | The 5 critical flows | 17 Oct | Medium |
| **Accessibility** | axe (Playwright) · manual TalkBack check | WCAG 2.2 AA basics; status never by colour alone | 16–17 Oct | Low |
| **Security** | Integration tests + scans | threat-model.md §6 | 16–17 Oct | Medium |
| **Resilience** | Simulator scenarios + manual | GPS loss, tunnel, provider down, display offline, crew offline queue | 16–18 Oct | Medium |

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

## 5. The 5 end-to-end flows

| # | Flow | Automated by | Key assertions |
|---|---|---|---|
| 1 | **Search → live delay** | Playwright (API) + Flutter integration | Search returns the journey; simulator delays it; `DelayChanged` received; app shows "6 min late" (fixture) within 1 min of the missed time (SM-2) |
| 2 | **Driver starts → train moves** in app and on platform display | Simulator as cab (API) + Playwright (display) + Flutter | `JourneyStarted`; positions accepted; platform display shows the train approaching; stop status changes |
| 3 | **Controller cancels** → push + display | Playwright (ops web → display) + backend push test | Confirmation shows impact; display shows "Cancelled" < 5 s (SM-3); push sent once to saved-trip holder; audit entry exists |
| 4 | **Platform change < 5 min with hold** | Playwright (ops web, displays) + Flutter (crew instruction) | Preview shows Late level and conflicts; hold set; old platform display redirects; info screen says "The train will wait"; crew instruction acknowledged |
| 5 | **Train delayed → provider gets new ETA** | Playwright (provider event log page) | Signed webhook received < 10 s (SM-5); stop provider, delay again, restart → delivered after retry, no duplicate |

Each flow records timestamps so SM-2, SM-3 and SM-5 are **measured**, not assumed.

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
