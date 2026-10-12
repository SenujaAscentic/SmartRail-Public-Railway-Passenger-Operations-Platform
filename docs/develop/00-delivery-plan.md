# SmartRail – Delivery Plan (whole application)

**Written:** 8 Oct 2026, 14:30 · **Re-planned:** 12 Oct 2026 · **Deadline:** demo and submission Tue 20 Oct 2026 · **Owner:** Senuja
**Builds on:** Delivery Timeline v2, `define/moscow.md`, Epic Breakdown (12 epics, apps × events matrix), ADR-01 to ADR-07, `develop/01-domain-model.md` (agreed 8 Oct).

---

## 1. Where we are

| Phase | Status | Output |
|---|---|---|
| Discover (1–6 Oct) | ✔ Done | Brief analysis, assumptions log, 22 stakeholder simulations |
| Define (6–7 Oct) | ✔ Done | Success measures, MoSCoW, personas, journeys, 40 stories, edge cases, epics |
| Develop – architecture (7–8 Oct) | ✔ Done | Tech stack ADR-01 to ADR-07, epic × app × event matrix, domain model (D-1 to D-11) |
| Develop – remaining (8–9 Oct) | ◐ In progress | API contract, auth design, spikes, repo scaffold, threat model, QA strategy, tokens, wireframes |
| Deliver (10–16 Oct) | Not started | Backend, ops web, passenger app, crew app, displays, catering |
| QA and wrap-up (17–20 Oct) | Not started | 5 E2E flows, security tests, defect log, AI usage report, demo |

**Time check:** 12 days remain, 7 of them for building, each app gets **one day**, and the only buffer is 16 Oct. The plan therefore protects three things above everything else:

1. A **walking skeleton** by the evening of 11 Oct, so every app is connected before any feature is built on top.
2. The **5 E2E flows**, because they prove the whole system works and are required by the brief (§23 QA).
3. **Explainability**: you must be able to explain any code in the review (§28). A day that produces code you can't explain is a day lost.

---

## 2. How each piece of work runs

### 2.1 The story loop (spec-driven + TDD for rules, ADR-08)

| Step | Who | Output |
|---|---|---|
| 1. Spec | Already done | Story, acceptance criteria, edge cases, rules (R-xx) |
| 2. Contract | You review | Endpoint and event shapes in `02-api-contract.md` |
| 3. Test names | AI drafts, **you approve** | Given/When/Then list per acceptance criterion; the domain model already has 44 for the rules |
| 4. Implement | Coding agent (Claude Code in VS Code) | Code until the tests pass |
| 5. Review | **You** + a second agent | Read the diff; ask the agent to explain anything unclear; independent review for rules and security code |
| 6. Record | You | Commit, regenerate `openapi.yaml`, AI log entry (accepted, changed, rejected) |

### 2.2 Daily rhythm (each build day)

| When | What |
|---|---|
| Start (30 min) | Write the day's task list from the epic's features; agree test names; check yesterday's open defects |
| Morning | Backend / shared parts of the day's epic (the producer side of its events) |
| Afternoon | The app's screens (the consumer side), tested against event fixtures |
| End (45 min) | Run all tests; click through the day's E2E flow by hand; update `openapi.yaml` diff, defect log, AI log; commit and tag `day-N` |

**Rule:** if a day's Musts are not done by its end, stop and re-plan with the cut list (section 7) before starting the next day, rather than borrowing from it silently.

### 2.3 Definition of done (every story)

- [ ] Acceptance criteria met, including the ★ moment-of-truth criteria.
- [ ] Unit tests for its rules and integration tests for its endpoints pass.
- [ ] Generated `openapi.yaml` diff reviewed and matches the contract doc.
- [ ] Role and scope checks in place; no personal data in logs or events.
- [ ] Status never shown by colour alone; screen-reader labels on new controls.
- [ ] AI usage log entry written.
- [ ] **You can explain the code without the AI.**

### 2.4 AI roles

| Agent / tool | Responsibility | How its output is checked |
|---|---|---|
| Claude (this project) | Design, documents, reviewing decisions, explaining trade-offs | You decide every proposal; decisions logged |
| Claude Code (VS Code) | Implementing stories against approved tests | Tests, your diff review, explanation on request |
| LangGraph + Gemini agents | Stakeholder simulations (done); **independent reviewers**: security reviewer, QA reviewer, accessibility reviewer | A model that did not write the code reviews it; you triage findings |
| Playwright + axe | E2E flows, accessibility scans | Run in CI; results kept as evidence |

---

## 3. Repository layout

One repository, one master copy of all docs.

```
smartrail/
  docs/
    discover/  define/  develop/  deliver/  qa/  security/  ai-usage/
  backend/
    src/SmartRail.Api/                 host, endpoints, SSE, auth
    src/SmartRail.Modules.Network/
    src/SmartRail.Modules.LiveOperations/
    src/SmartRail.Modules.Disruptions/
    src/SmartRail.Modules.Identity/
    src/SmartRail.Modules.Notifications/
    src/SmartRail.Modules.CateringIntegration/
    src/SmartRail.Simulator/           replays GPS through the same API as the cab tablet
    tests/  Unit/  Integration/  Contract/
    openapi.yaml                       generated, committed, diffed in CI
  provider/                            RailBites Catering service + own DB
  mobile/                              Flutter workspace
    apps/passenger/  apps/crew/
    packages/core/                     auth, API client (generated), SSE, widgets
  web/
    apps/ops-web/  apps/station-display/
    packages/ui/  packages/api-client/ packages/tokens/
  shared/
    tokens.json                        design tokens → CSS variables + Dart constants
    status-fixtures.json               input → expected text, used by every app's tests
    seed/                              network, timetable, staff, devices (verified data)
  e2e/                                 Playwright flows
  docker-compose.yml                   PostgreSQL ×2, backend, provider
  .github/workflows/ci.yml             build, tests, openapi diff, secret + dependency scan
```

---

## 4. Day-by-day plan (re-planned 12 Oct)

**Why re-planned:** design work (domain model, architecture discussion, API contract, threat model, QA strategy) ran until 12 Oct and no code existed on the morning of 12 Oct. Six build days remain (12–17 Oct, using Sat 17) instead of the original seven, and QA shrinks from two days to one. Scope was cut deliberately (section 7, MoSCoW change log 12 Oct) rather than discovered missing on 16 Oct.

**Develop – what happened to the remaining items:** API contract, auth design, threat model v1 and QA strategy v1 are done. Separate spikes are replaced by build steps that test the same risks with a fallback (SSE → walking skeleton on 13 Oct; FCM push → passenger day; cab GPS → crew day). Design tokens and status fixtures are made on 12 Oct; lo-fi wireframes are sketched at the start of each app's build day.

### Deliver – build (Mon 12 – Sat 17 Oct)

| Day | Epics | Work | Exit check |
|---|---|---|---|
| **Mon 12** | EP-10, EP-11 (part) | Repo scaffold (projects per CLAUDE.md §9, Docker Compose, CI, domain-purity architecture test); `shared/tokens.json` + `status-fixtures.json`; verified seed data (Main Line ~10 stations, both directions, services, train sets, staff, cab devices, Poya day); Network module; daily journey generation (B-2); staff and passenger sign-in (JWT) + role policies; audit writer | Journeys exist; staff sign-in returns a token; CI green |
| **Tue 13** | EP-9 | Position intake + simulator; rules R-01–R-06 test-first (delay, ETA, detection, source priority, freshness); outbox; SSE stream + snapshot + resume | **G2 – walking skeleton:** simulator delays a train → event reaches a bare ops-web page, a bare Flutter screen and a bare display page in < 5 s (this is the SSE spike) |
| **Wed 14** | EP-5, EP-6 | 30-min lo-fi sketch of dashboard + platform-change dialog; ops web: dashboard (alerts, disrupted trains, line view, section / station filters), source / freshness badges; delay, hold, cancel, reinstate, terminate, skip / add stop with confirmation and **control version**; platform change with preview, lead time, hold and conflict warning; crew messages; station announcements; audit log view | Flows 3 and 4 work from the ops side |
| **Thu 15** | EP-1, EP-2, EP-3 | Lo-fi sketch; passenger app: search, details + simple line diagram, live tracking (waiting / on board), disruptions + freshness, sign-up / sign-in, saved trip, push via FCM (fallback: in-app alerts only) | **Flow 1** works by hand |
| **Fri 16** | EP-7, EP-8 | Lo-fi sketches; crew app: cab mode start / end + GPS, glance view, Arrived / Departed, instructions with acknowledgement, phone-mode roster, simple offline queue; station displays: main board, platform display, info screen, offline banner + cached timetable | **Flow 2** works with the real cab app |
| **Sat 17** | EP-4, EP-11, EP-12 | Catering (light): provider service + own DB, signed webhooks with retries, menu / hotline cache, hotline screen in passenger app, provider health; hardening: rate limits, input validation, display read-only token, logs without personal data, health checks | **Flow 5** works · **G3 – feature freeze** in the evening |

### QA and wrap-up (Sun 18 – Tue 20 Oct)

| Day | Work | Output |
|---|---|---|
| **Sun 18** | Automate the 5 E2E flows (K-2 approach); security tests (permission matrix, lockout, tampered / replayed webhook, injection, display token cannot write); axe scans; bug fixing only; defect log; quality assessment against SM-1 – SM-5 | `qa/evidence/`, `qa/defect-log.md`, `qa/quality-assessment.md` · **G4:** 5 flows green |
| **Mon 19** | Architecture document; AI usage report (complete sections 6–7); screenshots as high-fidelity evidence; demo script; rehearsal + backup recording | `docs/` complete · **G5:** rehearsal done |
| **Tue 20** | Final check, present, submit | Submission |

---

## 5. Gates

| Gate | When | Pass condition | If it fails |
|---|---|---|---|
| G1 | ~~Fri 9 Oct~~ replaced | Contract agreed ✔; repo + CI green (Mon 12) | — |
| G2 | Tue 13 Oct, evening | Walking skeleton end to end | Do not start the ops web; fix the pipeline first. SSE fails → SignalR for web, polling for Flutter |
| G3 | Sat 17 Oct, evening | All remaining Musts done | Cut per section 7; record what was cut and why |
| G4 | Sun 18 Oct, evening | 5 E2E flows green | Demo the failing flow by hand and record it in the defect log honestly |
| G5 | Mon 19 Oct, evening | Rehearsed demo; backup video | Simplify the demo story |

---

## 6. Risks

| # | Risk | Likelihood / impact | Mitigation |
|---|---|---|---|
| K-1 | **Dart and Flutter are new** to you; passenger app day overruns | High / High | Spike on 9 Oct; shared `core` package built during the walking skeleton; cut P-9, S-9 first |
| K-2 | **Playwright cannot drive a native Flutter app**, but flows 1–4 involve the passenger or crew app | Certain / High | Decide in the QA strategy (9 Oct). Proposal: Playwright drives the web apps and asserts the event on the stream and the API state the mobile app reads; Flutter integration tests prove the app renders that event (fixtures). Optional: Patrol for one real-device flow if time allows |
| K-3 | One day per app, no buffer before 16 Oct | High / High | Gates, cut list, the end-of-day re-plan rule |
| K-4 | Real station data invented or wrong (AI output) | Medium / Medium | Verify against an authoritative source on 10 Oct; record the source |
| K-5 | Generated code you cannot explain (§28) | Medium / High | Definition of done includes "can explain it"; daily walkthrough of the most complex diff |
| K-6 | Emulator, FCM or Docker setup eats a day | Medium / High | All environment work in the 9 Oct spikes, not on build days |
| K-7 | Docs and AI log left until 19 Oct and rebuilt from memory | Medium / High | AI log and defect log updated every day at the end-of-day step |

---

## 7. Cut list

**Already cut on 12 Oct (re-plan):** O-8 unscheduled stop alert; O-6 + C-5 crew incident reports and the incident workflow; all Shoulds (P-9, S-1 … S-11); station manager limited to announcements (O-9). Recorded in the MoSCoW change log.

**If still behind, cut next in this order:**

1. Polish inside Musts: line-diagram animation, rotating content, phone-mode roster details.
2. O-7 crew messages reduced to the hold instruction only (needed by flow 4).
3. Crew offline queue reduced to Arrived / Departed only.

**Never cut:** the 5 E2E flows, authentication with role + scope, the audit log, the catering integration (§23), the AI usage log.

---

## 8. Assessment artifacts (§23) – where and when

| Area | Artifact | Location | When |
|---|---|---|---|
| Requirements | Refined requirements, assumptions, personas, journeys, stories, acceptance criteria, edge cases | `docs/discover`, `docs/define` | ✔ |
| Product and UX | User flows, wireframes, design-system definition, accessibility considerations, hi-fi (screenshots of built UI) | `docs/develop/03-wireframes`, `shared/tokens.json`, `docs/deliver/screens` | 9 Oct, 19 Oct |
| Architecture | Architecture, boundaries, components, data ownership, integration, auth approach, ADRs | Tech stack ADRs, `01-domain-model.md`, `02-api-contract.md`, `docs/develop/architecture.md` | ✔ / 8 Oct / 19 Oct |
| Backend | Working backend | `backend/` | 10–16 Oct |
| Web, mobile, station, integration | Ops web, passenger app, crew app, displays, provider service | `web/`, `mobile/`, `provider/` | 12–15 Oct |
| QA | Strategy, functional scenarios, 5 automated flows, defects, evidence, quality assessment | `docs/qa/`, `e2e/` | 9 Oct (v1), 17–18 Oct |
| Security | Threat model, decisions, risks, controls, security test evidence | `docs/security/` | 9 Oct (v1), 16–17 Oct |
| AI usage | Tools, responsibilities, prompts and workflows, validation, AI mistakes, own judgment, modified code | `docs/ai-usage/` | Daily; report 19 Oct |

---

## 9. Decisions to record now

| ID | Decision | Where |
|---|---|---|
| ADR-08 | Testing approach: spec-driven stories, TDD for domain rules, tests after UI, 5 E2E flows from ★ criteria | Tech stack decisions |
| ADR-09 | API contract: design-first document, code-first `openapi.yaml` generated and diffed in CI | Tech stack decisions |
| — | Build order (producers before consumers) differs from app priority, with the reason in section 4 | This plan |
