# SmartRail – Instructions for coding agents

You are helping one developer (Senuja) build SmartRail, an assessed MVP due **20 Oct 2026**. He must be able to explain every line in a review, so **clarity beats cleverness**, and **he makes the decisions**. When something is not covered here or in the docs, stop and ask; do not guess.

## 1. Read before coding

The specs are the source of truth. Code follows them, never the reverse.

| Need | Read |
|---|---|
| What a feature must do | `docs/define/user-stories.md` (acceptance criteria, ★ = must never fail), `docs/define/edge-cases.md` |
| Scope (is it even in?) | `docs/define/moscow.md`. Not a Must / agreed Should → don't build it |
| Entities, states, rules R-01–R-22, events | `docs/develop/01-domain-model.md` |
| Endpoints, auth, errors, stream | `docs/develop/02-api-contract.md` |
| Architecture rules and why | `docs/develop/03-architecture-principles.md` |
| Threats and required controls | `docs/security/threat-model.md` |
| Test approach | `docs/qa/strategy.md` |

If code and spec disagree, **stop and report the difference**. Do not "fix" the spec yourself.

## 2. Scope (MVP)

One line (Main Line, both directions), one **Controller** role, station managers per station, drivers and guards. No regions, no chief / regional controllers, no interchanges, no in-app food ordering, no ticketing.

## 3. Architecture rules (hard)

- **Modular monolith.** Modules: `Network`, `LiveOperations`, `Disruptions`, `Identity`, `Notifications`, `CateringIntegration`. Each owns its own PostgreSQL schema.
- **Never query or reference another module's tables or internals.** Use its public interface (direct call) or data carried in an event. NetArchTest enforces this; never weaken or delete that test.
- **Dependencies point one way:** Disruptions → LiveOperations is allowed; LiveOperations → Disruptions is not.
- **Direct call vs event:** need an answer, or a decision that must succeed or fail together → direct call (same transaction). Announcing a fact → domain event via the **outbox**, written in the same transaction as the state change.
- **Inside a module:** feature slices, one folder per feature (endpoint, handler, validation, tests), e.g. `LiveOperations/Features/CancelJourney/`. Handlers may use EF Core directly; no repository layer, no mediator library.
- **Pure domain projects:** `SmartRail.LiveOperations.Domain` and `SmartRail.Disruptions.Domain` hold aggregates, value objects and rules R-xx. They must reference **no** EF Core, ASP.NET Core, HTTP or logging packages and never read the clock (time is passed in). Never add such a reference to make something compile; put that code in the slice instead. Other modules have no domain project; keep their small rules in plain classes with unit tests.
- **Business rules live only in the backend.** Apps display what they receive (with age labels) and never recalculate delay, ETA or status.
- **Time:** inject `TimeProvider`; never `DateTime.Now` / `UtcNow`. Store and send UTC; apps format in Asia/Colombo.

## 4. Domain rules that are easy to get wrong

- **Control version (R-18):** only **decisions** (delay, hold, release, cancel, reinstate, terminate, skip / unskip / add stop, platform change, controller time correction) bump `controlVersion` and require `expectedVersion`. GPS, detection, crew taps and ETA recalculation **must not** bump it. A stale decision → `409 VERSION_CONFLICT`.
- **Source priority (R-05):** Controller > Crew > GPS > Schedule. The **server** sets `source` from the credential; never read it from a request body.
- **Delay (R-01):** whole minutes, rounded **down**; negative = early. Predicted departures never earlier than scheduled (R-03).
- **Lead time (R-11):** expected **arrival** − now (origin: expected departure − now). "Train will wait" text only when a hold is actually set.
- **Positions (R-07, R-08):** only from a registered cab device with an active cab session on a running, assigned journey; discard older-than-last, > 500 m off route, > 120 km/h.
- **Crew offline actions:** keep their recorded time; `clientActionId` makes repeats harmless; never overwrite newer or higher-priority values.

## 5. How to work

1. **Domain rules: test first.** Use the approved test names in the domain model §10. Write the failing test, then the code. Use `FakeTimeProvider` for anything time-based.
2. **Endpoints:** implement exactly as in the contract (paths, fields, policies, error `code`s); add integration tests with Testcontainers (real PostgreSQL). Every new endpoint gets rows in the **permission matrix test** (every role, expected allow / deny).
3. **After API changes:** regenerate `backend/openapi.yaml` and show the diff. Any difference from `02-api-contract.md` must be pointed out, not silently kept.
4. **Client types (C-8):** TypeScript types are **generated** with `openapi-typescript` and called with `openapi-fetch`; never hand-write API types in the web apps. Dart: use a generator only if its output is clean and readable; otherwise hand-written models **plus** contract tests that parse the examples in `openapi.yaml`.
5. **Events:** every SSE event type has one example JSON in `shared/events/`. Backend, web and Flutter tests parse these files. Change the example in the same commit as the event.
6. **Status text** ("3 min late", "Platform 5 (was 2)") comes only from `shared/status-fixtures.json`; every app's tests run those fixtures.
7. **Small steps:** one story or rule at a time; keep each change reviewable. Explain non-obvious code in a short comment or in your summary.

## 6. Security rules

- No personal data (names, emails, phones, tokens) in logs, events, URLs or query strings.
- Passwords via ASP.NET Core Identity only; never custom hashing.
- Announcements and notes are plain text; never render as HTML.
- Display-device tokens are read-only; every write endpoint must refuse them (tested).
- No secrets in the repo; use user-secrets / environment variables.
- EF Core parameterised queries only; no string-built SQL.

## 7. Stop and ask Senuja before you

- add **any** dependency (NuGet, npm, pub): name it, say why, and confirm it is the official, maintained package;
- change the API contract, an event shape, the database schema of another module, or a rule R-xx;
- skip, weaken, delete or mark-as-ignored any test;
- build anything not in the MoSCoW Musts / agreed Shoulds;
- make a design choice the docs don't cover.

## 8. Definition of done (per story)

- [ ] Acceptance criteria met, including ★ criteria
- [ ] Unit + integration tests pass; permission matrix updated
- [ ] `openapi.yaml` regenerated and matches the contract
- [ ] No personal data in logs / events; status never shown by colour alone
- [ ] Summary written for Senuja: what changed, why, anything uncertain

## 9. Repository layout

```
docs/                     specs (read-only for agents unless asked)
backend/src/              SmartRail.Api (host, auth, SSE, outbox dispatcher)
                          SmartRail.LiveOperations.Domain, SmartRail.LiveOperations
                          SmartRail.Disruptions.Domain, SmartRail.Disruptions
                          SmartRail.Network, .Identity, .Notifications, .CateringIntegration
                          SmartRail.Simulator
backend/tests/            Unit / Integration / Contract / Architecture
provider/                 RailBites catering service (separate DB)
mobile/apps/{passenger,crew}, mobile/packages/core
web/apps/{ops-web,station-display}, web/packages/{ui,api-client,tokens}
shared/                   tokens.json, status-fixtures.json, events/, seed/
e2e/                      Playwright flows
```
