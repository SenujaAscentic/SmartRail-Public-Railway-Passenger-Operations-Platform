# SmartRail – Documentation Index

**What this is:** a map of every SmartRail document. Each question has **one** document that answers it.
**Master copy:** this repository (`docs/`). Copies elsewhere are snapshots.
**Process:** Double Diamond: Discover → Define → Develop → Deliver, with AI assistance at every step and every decision logged (brief §21).
**Deadline:** demo and submission Tue 20 Oct 2026.

---

## Which document answers which question

| Question | Document | Status |
|---|---|---|
| What does the brief say, and what does it leave out? | `discover/00-brief-analysis.md` (analysis, assumptions log, glossary) | ✔ Final |
| **What are we building?** (Must / Should / Could / Won't, success measures) | `define/moscow.md` | ✔ Final, 6 Oct |
| For whom, and what is their experience today and tomorrow? | `define/personas-and-journeys.md` | ✔ Final |
| How exactly should each feature behave? | `define/user-stories.md` (40 stories, acceptance criteria) | ✔ Final |
| What can go wrong at the edges? | `define/edge-cases.md` (E-01–E-46) | ✔ Final |
| How is the work grouped? | `pages/smartrail-epics.html` (12 epics, 52 features, apps × events matrix) | ✔ Final |
| Which technologies, and why? | `develop/04-technology-stack-decisions.md` (ADR-01–ADR-07) | ✔ Final; ADR-08, ADR-09 to add |
| When is each thing built? | `develop/00-delivery-plan.md` (day plan, gates, risks, cut list) | ✔ Final |
| What data and rules sit behind the features? | `develop/01-domain-model.md` (entities, state machines, rules R-01–R-22, events, 44 test names) | ✔ Agreed (D-1–D-11) |
| How do the apps talk to the backend? | `develop/02-api-contract.md` (endpoints, sign-in, stream, errors) | ◐ Decisions C-1–C-8 open |
| How is the system structured, and why? | `develop/03-architecture-principles.md` (quality goals, modular monolith, communication, logic and data rules) | ✔ Agreed (Q1–Q5); Q6–Q8 proposed |
| How do we keep it safe? | `security/threat-model.md` (trust boundaries, 20 threats, controls by build day) | ◐ v1; evidence 16–17 Oct |
| How do we prove it works? | `qa/strategy.md` (test levels, 5 E2E flows, defects) | ◐ v1; K-2 open |

**Rule of thumb:** *what* and *why* live in `define/`; *how* lives in `develop/`, `security/` and `qa/`. Design documents never add features. A new feature means changing `define/moscow.md` first.

---

## Open decisions

| ID | Question | Document | Needed by |
|---|---|---|---|
| C-1–C-7 | API conventions, tokens, status formatting, platform preview, story/model mismatches M-1–M-4 | `develop/02-api-contract.md` §8–9 | Sat 10 Oct morning |
| C-8 | How clients get API types (code generation) | `develop/02-api-contract.md` §9 | Sun 11 Oct morning |
| K-2 | Automating flows through the native mobile apps | `qa/strategy.md` §4 | Sun 11 Oct |
| Q6–Q8 | Code structure, catering boundary, scaling later | `develop/03-architecture-principles.md` | Sat 10 Oct morning |

---

## Where decisions are recorded

| Kind | Where |
|---|---|
| Assumptions (A-xx) | `discover/00-brief-analysis.md` |
| Technology decisions (ADR-xx) | `develop/04-technology-stack-decisions.md` |
| Domain decisions (D-x) | `develop/01-domain-model.md` §11 |
| Architecture decisions (Q1–Q8) | `develop/03-architecture-principles.md` |
| API decisions (C-x) | `develop/02-api-contract.md` §9 |
| AI usage (accepted, changed, rejected) | AI usage log section at the end of each document → collected in `ai-usage/` on 19 Oct (current log: `ai-usage/ai-usage-log.md`) |

---

## Assessment artifacts (brief §23) → documents

| §23 area | Documents |
|---|---|
| Requirements | `discover/`, `define/` |
| Product and UX | journeys, wireframes and design tokens (9 Oct onward), screenshots (19 Oct) |
| Architecture | `develop/04-technology-stack-decisions.md` (ADRs), `develop/01`–`03`, architecture document (19 Oct) |
| Backend, web, mobile, station, integration | Source code (10–16 Oct) |
| QA | `qa/` |
| Security | `security/` |
| AI usage | AI log sections, `ai-usage/` report (19 Oct) |
