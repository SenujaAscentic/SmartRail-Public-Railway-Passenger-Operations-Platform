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
| **What are we building?** (Must / Should / Could / Won't, success measures) | `define/moscow.md` | ✔ Final 6 Oct; scope changes 9 and 12 Oct in its change log |
| For whom, and what is their experience today and tomorrow? | `define/personas-and-journeys.md` | ✔ Final |
| How exactly should each feature behave? | `define/user-stories.md` (stories, acceptance criteria) | ✔ Final (deferred stories marked) |
| What can go wrong at the edges? | `define/edge-cases.md` (E-01–E-46) | ✔ Final |
| How is the work grouped? | Epic Breakdown page (`pages/smartrail-epics.html`: epics, features, apps × events matrix) | ✔ Final |
| Which technologies, and why? | `develop/04-technology-decisions.md` (ADR-01–ADR-11) | ✔ Final 12 Oct |
| When is each thing built? | `develop/00-delivery-plan.md` (day plan, gates, risks, cut list) | ✔ Re-planned 12 Oct |
| What data and rules sit behind the features? | `develop/01-domain-model.md` (entities, state machines, rules R-01–R-22, events, 48 test names) | ✔ Agreed (D-1–D-11) |
| How do the apps talk to the backend? | `develop/02-api-contract.md` (endpoints, sign-in, stream, errors) | ✔ Agreed (C-1–C-8) |
| How is the system structured, and why? | `develop/03-architecture-principles.md` (quality goals, modular monolith, communication, logic, data, code structure) | ✔ Agreed (Q1–Q6); Q7–Q8 proposed |
| How do we keep it safe? | `security/threat-model.md` (trust boundaries, 20 threats, controls by build day) | ◐ v1; evidence 17–18 Oct |
| How do we prove it works? | `qa/strategy.md` (test levels, 5 E2E flows, defects) | ◐ v1; K-2 open |
| How was AI used? | `ai-usage/ai-usage-log.md` | ◐ Updated daily |
| How does the coding agent work? | `../CLAUDE.md` (repo root), `.claude/agents/spec-reviewer.md` | ✔ |

**Rule of thumb:** *what* and *why* live in `define/`; *how* lives in `develop/`, `security/` and `qa/`. Design documents never add features. A new feature means changing `define/moscow.md` first.

---

## Open decisions

| ID | Question | Document | Needed by |
|---|---|---|---|
| K-2 | Automating flows through the native mobile apps | `qa/strategy.md` §4 | Sun 18 Oct |
| Q7–Q8 | Catering boundary, scaling later | `develop/03-architecture-principles.md` | Sat 17 Oct |

---

## Where decisions are recorded

| Kind | Where |
|---|---|
| Assumptions (A-xx) | `discover/00-brief-analysis.md` |
| Technology decisions (ADR-xx) | `develop/04-technology-decisions.md` |
| Domain decisions (D-x) | `develop/01-domain-model.md` §11 |
| Architecture decisions (Q1–Q8) | `develop/03-architecture-principles.md` |
| API decisions (C-x) | `develop/02-api-contract.md` §9 |
| AI usage (accepted, changed, rejected) | AI usage log section at the end of each document → collected in `ai-usage/` on 19 Oct |

---

## Assessment artifacts (brief §23) → documents

| §23 area | Documents |
|---|---|
| Requirements | `discover/`, `define/` |
| Product and UX | journeys, wireframes and design tokens (9 Oct onward), screenshots (19 Oct) |
| Architecture | Tech Stack ADRs, `develop/01`–`03`, architecture document (19 Oct) |
| Backend, web, mobile, station, integration | Source code (10–16 Oct) |
| QA | `qa/` |
| Security | `security/` |
| AI usage | AI log sections, `ai-usage/` report (19 Oct) |