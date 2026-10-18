# SmartRail – Architecture Principles

**Phase:** Develop · **Agreed:** 8–9 Oct 2026 in an open architecture discussion (questions 1–5) · **Owner:** Senuja
**Related:** ADR-01 to ADR-07 (Technology Stack Decisions), `01-domain-model.md`, `02-api-contract.md`.

Each principle records the question, my decision and the reasoning, because the review (§28) asks *why*, not only *what*.

---

## Q1. What the architecture optimises for

**Constraints (not negotiable; every option is filtered by them):** one developer · 7 build days · every part explainable in the review · free tools.

**Quality goals, ranked:**

| Rank | Goal | Evidence |
|---|---|---|
| 1 | **Real-time speed** | SM-2 (delay shown < 1 min), SM-3 (controller action everywhere < 5 s); platform change with < 5 min notice |
| 2 | **Reliability:** no lost or wrong information | A missing cancellation breaks trust more than a slow one; SM-5 requires 100% delivery to the provider |
| 3 | **Security** | Controllers can cancel trains; display devices are public; passenger personal data (§17) |
| Lowest | **Scalability:** don't build it, don't block it | Writes are small (positions from a few hundred trains); the large number is read connections, solvable later by adding instances |

**Changed during discussion:** I first ranked scalability third and simplicity last. I swapped scalability for reliability (wrong information is worse than slow information), and moved simplicity out of the ranking into the constraints, because it is a limit on the project, not a quality the railway asks for.

---

## Q2. Deployment shape: modular monolith

| Option | Verdict | Reason |
|---|---|---|
| Plain monolith | Rejected | No enforced boundaries; AI-generated code drifts into one tangle that is hard to explain |
| Microservices | Rejected | Fails the time constraint; "change + events together or not at all" across services needs extra patterns (hurts reliability) |
| Serverless functions | Rejected | Cannot hold long-lived SSE connections (hurts real-time); background timers are awkward |
| **Modular monolith** | **Chosen** | Long-lived connections; one transaction for state + events; one program to run and explain; module boundaries can become services later |

---

## Q3. How the parts talk

| Situation | Use | Example |
|---|---|---|
| Need data back now | **Direct call** to a module's public read interface | Live Operations → Network: station zone for arrival detection |
| A command the user waits on that must succeed or fail together with the caller's own change | **Direct call inside one transaction** | Disruptions creates an incident and calls Live Operations `ApplyDelay` in the same transaction; if the delay is refused, the incident is rolled back and the controller is told |
| Announcing a fact; reactions can happen a moment later and retry on their own | **Event** via the outbox | `JourneyCancelled` → push, stream, catering |
| Apps → backend | REST commands and queries | Cancel, search |
| Backend → apps | SSE stream of events | Live updates |

- **Dependencies point one way.** Disruptions may call Live Operations, never the reverse; Live Operations stores only `incidentId`.
- **Recorded architecture change (18 Oct 2026): Live Operations → Identity is now an allowed dependency**, through Identity's public `IStaffDirectory` only (staff number → a driver's account id), used when journeys are given their driver. **Reason:** R-07 accepts positions only from a journey's assigned driver, and the cab token identifies the driver by account id (`sub`); no module may read another's tables, so the id must come through Identity's public door. The alternative, an adapter in the host, was not chosen because it adds code for no gain. **One-way:** Identity depends only on Network, never on Live Operations, so there is no cycle. The architecture test's list of allowed pairs is now Disruptions → Live Operations, Live Operations → Network, Identity → Network, Live Operations → Identity.
- **Changed during discussion:** for case 3 I first chose events. Walking through a refused delay showed that an event cannot say "no" back, leaving an incident that claims a delay that never happened. That is a reliability failure, so it became a direct call in one transaction.

---

## Q4. Where the logic lives

| Side | Pattern | Device may | Only the backend |
|---|---|---|---|
| **Reading** (passenger app, ops web, displays) | Backend calculates; apps show it with honest age labels | Show the last known value; count "updated X min ago" with its own elapsed-time stopwatch (not by comparing clocks); on reconnect fetch a snapshot, then resume the stream | Delay, ETA, status, all business rules |
| **Writing** (crew app) | Offline-first queue | Accept the tap at once as **pending** ("Arrived 10:12 · waiting to send"); stamp the tap time (clock-corrected, R-09); give it an idempotency key; resend in order | Decide which value wins (source priority Controller > Crew > GPS); recalculate; tell every app |

- **Two kinds of old data are shown differently:** "You're offline" (the phone has no connection) vs "Live location unavailable" (the train stopped sending). The first is known by the app, the second by the backend.
- **A queued crew tap that loses to a controller value is kept** in history and audit; the crew app replaces its pending value with the winning one on reconnect.
- **The one deliberate exception:** offline station displays remove departed trains using the cached timetable and their own clock, while showing "Live information unavailable" (US-D4).

---

## Q5. Data ownership

- One PostgreSQL database, **one schema per module** (`network`, `ops`, `disruptions`, `identity`, `notifications`, `catering`).
- **No module queries another module's tables.** It uses the other module's public read interface, or the facts carried in an event.
- **Journey events carry a small summary** (service number, route name, affected stops with names and times), so consumers such as push, displays and catering need no extra lookup. The message then describes what was true when the event happened.
- **Enforced by an architecture test** (NetArchTest) that fails the build if one module references another module's internals. This matters because a coding agent will take the shortest path (a cross-module join) unless something stops it.

**Why (from the discussion):** a cross-module join works on day one, then breaks silently when the owning module changes its tables, or returns values that differ from what the app shows, and makes the module impossible to split later.

---

## Q6–Q8. Code structure (Q6 agreed 12 Oct after discussion); Q7–Q8 proposed defaults

| # | Question | Proposal | Why |
|---|---|---|---|
| Q6 ✔ | Code structure inside a module | **Agreed 12 Oct:** feature slices in every module (one folder per feature: endpoint, handler, validation, tests). Modules with real business rules (**LiveOperations, Disruptions**) also get a separate **pure `*.Domain` project** (aggregates, value objects, rules R-xx; no EF Core, ASP.NET or clock references, enforced by the compiler). Simple modules (Network, Identity, Notifications, CateringIntegration) have no domain project | Structure in proportion to complexity. Keeps Clean Architecture's dependency rule where the safety-relevant rules live; a compile error gives the coding agent immediate feedback if it leaks infrastructure into rules. Full Clean Architecture (~24 projects, 4 files per feature, repositories wrapping EF Core) rejected: cost without protection for read-only lookups and plumbing |
| Q7 | Catering boundary | SmartRail never depends on the provider being up: menus are cached; events wait in the outbox; the provider's station IDs are mapped at the boundary (anti-corruption layer); no passenger data crosses | A provider outage must never block a controller action or a passenger screen (reliability) |
| Q8 | Later, at national scale | Add instances behind a load balancer with a shared event bus (e.g. Redis pub/sub) for SSE fan-out; cache timetable reads at a CDN; read replicas for heavy queries; split a module into a service only where load proves it | The seams exist now (modules, events, stateless API); nothing is built before it is needed |
