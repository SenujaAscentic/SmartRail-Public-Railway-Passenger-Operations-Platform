# SmartRail – Technology Stack Decisions

7 Oct 2026, updated 12 Oct 2026 · @Senuja Bodhinayake

## Summary

SmartRail runs on an ASP.NET Core (C#) backend with PostgreSQL, Flutter for the passenger and crew apps, and React for the operations web app and station displays. The catering provider is a separate service with its own database. Every choice below is traced to requirement IDs in `define/moscow.md`, `define/user-stories.md` and the assumptions log.

**Decision method (same for every layer):**

1. List the drivers: requirements that constrain the technology (with IDs).
2. Eliminate options that fail a hard constraint.
3. Compare the remaining options on the drivers, including my own skills and the build window.
4. Validate the riskiest assumption early (a spike, or the first build step that exercises it), with a fallback ready.
5. Record the decision, alternatives and consequences as an ADR, and log where AI suggestions were accepted or overridden.

| Layer | Choice | Main reason | Status |
| --- | --- | --- | --- |
| Backend | ASP.NET Core (C#), modular monolith | Built-in real-time, background jobs, policy authorisation and `TimeProvider`; my C# fluency | Decided (ADR-01) |
| Live update transport | Server-Sent Events (default), SignalR as alternative | One-way stream fits the event model; no fragile Flutter client library | Decided; **validated by the walking skeleton on 13 Oct** (ADR-02) |
| Database | PostgreSQL + EF Core, one schema per module | Relational domain, transactions, outbox, free at national scale | Decided (ADR-03) |
| Mobile | Flutter, Android-first | Mature push and location plugins; in training scope | Decided (ADR-04) |
| Crew device model | Driver phone (roster) + registered cab tablet (operate, GPS) | No GPS on personal phones; train-powered, kiosk device | Decided (ADR-05) |
| Web front end | React + TypeScript (Vite), two apps | Largest ecosystem, in training scope, strong AI support | Decided (ADR-06) |
| Catering provider | Separate ASP.NET Core service + own database | Brief §12 requires a separate external system | Decided (ADR-07) |
| Testing approach | Spec-driven features, TDD for domain rules, 5 E2E flows | Rules are where AI-written code fails silently | Decided (ADR-08) |
| API contract | Design-first document; `openapi.yaml` generated from code and checked in CI; generated client types | One source of truth across C#, TypeScript and Dart | Decided (ADR-09) |
| Authentication | ASP.NET Core Identity + backend-issued JWT; no full OAuth 2.0 in the MVP | All clients are first-party | Decided (ADR-10) |
| Backend code structure | Feature slices; pure `Domain` projects for rule-heavy modules | Structure in proportion to complexity; compiler guards the rules | Decided (ADR-11) |
| Mobile architecture | `ChangeNotifier`, `Navigator`, `http`, hand-written models, shared `core` package | Built-in Flutter only; readable in a review | Decided (ADR-12) |
| Testing tools | xUnit + Testcontainers, Vitest, Flutter tests, Playwright + axe | One tool per layer; E2E across apps | Decided |

**Solution architecture:** 4 clients, 1 backend, 2 databases (diagram: `develop/03-architecture-principles.md`).

The catering provider is reached only through its API and signed events; SmartRail never reads the provider's database.

## ADR-01 Backend: ASP.NET Core (C#)

**Decision:** ASP.NET Core (current LTS) as a modular monolith: one deployable application with internal modules for Network, Live Operations, Disruptions, Identity, Notifications and Catering Integration.

**Drivers:**

- One event stream pushed to every app within 5 s (B-5, SM-3).
- Always-running background work: stale / lost GPS timers (A-26), daily journey generation (B-2), provider event retries (F-2).
- Role + scope authorisation on every request (O-1, B-7, E-20).
- Controllable clock for tests (A-106).
- Concurrent edits refused (A-66), rate limits (US-P5, Q-2), structured logs and health checks (Q-3).
- Must be explainable and modifiable live during review (§28) within the build window.

**Options considered:**

| Option | Result | Reason |
| --- | --- | --- |
| Next.js | Eliminated | API routes are request/response, usually serverless: no long-lived push connections, no persistent process for timers and retries |
| Node.js (TypeScript) | Viable, not chosen | Equal on real-time; would share TypeScript with React, but needs libraries for jobs and authorisation, and C# is my stronger language |
| ASP.NET Core | **Chosen** | Built-in features map directly to the drivers (below); I am fluent in C# |

**How built-in features meet the drivers:**

| Requirement | ASP.NET Core feature |
| --- | --- |
| Live push (B-5) | SSE results or SignalR (see ADR-02) |
| Timers, generation, retries (A-26, B-2, F-2) | `BackgroundService` / hosted services |
| Role + scope (O-1, B-7) | Policy-based and resource-based authorisation |
| Controllable clock (A-106) | `TimeProvider`, `FakeTimeProvider` in tests |
| Validation and errors | Model validation + ProblemDetails |
| Rate limits (US-P5) | Rate-limiting middleware |
| Concurrency (A-66) | EF Core concurrency tokens (internal `rowVersion`) + a domain **control version** for decisions (R-18) |
| Logs, health (Q-3) | `ILogger` structured logging, health checks, OpenTelemetry |

**Why a modular monolith, not microservices:** one engineer, one week. Module boundaries keep the code maintainable; a single deployable keeps transactions, debugging and the demo simple. Modules can be split later where load requires it. The full reasoning (quality goals, options rejected) is in `develop/03-architecture-principles.md` Q1–Q2.

**Consequences:**

- No shared types with the TypeScript and Dart front ends; mitigated by generating client types from the OpenAPI spec (ADR-09).
- .NET is not in the training list (§24); the brief allows free choice, and the reasons above justify it.
- Scaling to several instances later needs a shared event bus (Redis); not needed for the MVP.

## ADR-02 Live update transport: SSE by default

**Decision:** push live events from server to clients with Server-Sent Events (SSE). All client actions (cancel, Arrived, incident report) stay normal REST calls. SignalR remains the alternative if validation shows a reason to prefer it.

**Drivers:** one event stream to passenger app, crew app, ops web and displays (B-5, A-73); action → every client in < 5 s (SM-3); full state reload on reconnect (A-102); displays keep working offline (US-D4).

**Why SSE fits:** the event stream is one-way (server → clients). SSE is plain HTTP, so browsers and Flutter read it as a streamed HTTP response. No special client library is needed on any of the three front-end technologies.

| Option | Strengths | Weaknesses |
| --- | --- | --- |
| **SSE** | Plain HTTP; no client library; native browser support; simple to test | Server → client only (fine here); groups and reconnect logic written by me |
| SignalR | Groups, automatic reconnect, scale-out backplane; official .NET and JavaScript clients | No official Flutter client: community packages are fragmented into several forks, with weak maintenance scores and known callback issues ([depscope](https://mcp.depscope.dev/pkg/pub/signalr_netcore), [pub.dev](https://pub.dev/documentation/signalr_netcore3/latest/)) |

**What I build myself with SSE:** a filter per client (station, journey, line), a sequence number per event, and "reconnect → fetch snapshot → resume stream", which A-102 requires anyway.

**Authentication on the stream (12 Oct):** the browser's built-in `EventSource` cannot send an `Authorization` header, and tokens in URLs leak into logs, so the web apps read the stream with a fetch-based SSE client that sends the header; Flutter sends it directly (API contract C-6).

**Validation (updated 12 Oct):** the separate spike planned for 8–9 Oct is replaced by the **walking skeleton on 13 Oct**: a simulator-driven delay must reach a bare ops-web page, a Flutter screen and a display page in < 5 s. **Fallback:** SignalR for the web apps and polling for Flutter.

**Consequences:** less framework help with connection groups; scale-out later needs a shared event bus (e.g. Redis pub/sub) behind the SSE endpoints.

## ADR-03 Database: PostgreSQL + EF Core

**Decision:** PostgreSQL accessed through EF Core (Npgsql), running in Docker locally and in tests. **One schema per module** (`network`, `ops`, `disruptions`, `identity`, `notifications`, `catering`); no module queries another module's tables (architecture Q5).

**Drivers:**

| Requirement | Database implication |
| --- | --- |
| Station → route → service → scheduled stop → journey → stop status; crew and train set assignments | Relational, with foreign keys and constraints |
| A controller action updates journey, stop statuses and events together (O-4) | Transactions across tables |
| 100% of events reach the provider, with retries (F-2, SM-5) | Transactional outbox in the same database |
| Concurrent edits refused (A-66) | Optimistic concurrency (row version + control version) |
| Audit log cannot be edited or deleted (O-10) | Append-only table; app user has insert-only rights |
| Positions every few seconds; raw kept 30 days, then aggregated (§20) | Time-series table partitioned by day |
| Overnight journeys stay on their service date (A-18) | Service date + time offsets; `timestamptz` for actual times |
| Integration tests on a real database (Q-1) | Docker / Testcontainers |

**Options considered:**

| Option | Result | Reason |
| --- | --- | --- |
| **PostgreSQL** | **Chosen** | Free at any scale (matters for a national operator), excellent EF Core support, partitioning, PostGIS available later |
| SQL Server | Viable, not chosen | First-party EF Core and temporal tables, but paid licence in production; Express is capped at 10 GB |
| MongoDB | Eliminated | The domain is relationships and integrity; transactions and constraints would be rebuilt in code |

**Deliberately not added in the MVP:**

- **Redis:** one backend instance keeps running journeys' live state in memory and persists changes. Added later as an event bus for scale-out.
- **PostGIS:** station zones (300 m) and off-route checks (500 m) are simple distance maths in C#, easier to unit-test with the controllable clock. PostGIS is the upgrade path.

**All data stores in the solution:**

| Where | Store | Holds |
| --- | --- | --- |
| SmartRail backend | PostgreSQL, database `smartrail`, one schema per module | Network, timetable, journeys, live status, accounts, audit log, positions, outbox |
| Catering provider | PostgreSQL, its own database and credentials | Menus, availability, hotline, received events log |
| Crew app | SQLite (drift) | Today's roster, offline action and position queue (C-7) |
| Passenger app | SQLite or key-value store | Last known data with its age (§16), saved trips cache |
| Station displays | Browser IndexedDB / Cache API | The day's timetable for offline mode (US-D4) |

**Consequences:** no built-in history tables, so the audit log is designed explicitly; partition maintenance (create and drop daily partitions) runs as a background job.

## ADR-04 Mobile: Flutter, Android-first

**Decision:** both the passenger app and the crew app are built with Flutter in one workspace, sharing a package for sign-in, API client, live stream and design-system widgets. Android is the MVP target platform.

**Drivers:**

| Need | Requirement |
| --- | --- |
| Location every 10–30 s during a journey on the cab device | C-2, B-3 |
| Push notifications for saved trips | P-7 |
| Live updates from the .NET backend | P-3, P-4, C-6 |
| Offline action queue with timestamps | C-7 |
| Custom line diagram with a moving train | P-2, A-09 |
| Dark landscape crew UI; screen readers, text scaling | C-3, Q-4 |
| Two apps sharing sign-in, API client, live updates, components | moscow §3 |

**Options considered:**

| Option | Result | Reason |
| --- | --- | --- |
| **Flutter** | **Chosen** | Mature push (FlutterFire) and location plugins; `CustomPainter` for the line diagram; in training scope (§24); Dart is easy to learn from C# |
| .NET MAUI | Viable, not chosen | One language with the backend and an official SignalR client; actively developed by Microsoft ([issoh.co.jp](https://www.issoh.co.jp/tech/details/7938/)), but push and background location rely on community plugins or custom Android code, a schedule risk in a short build |
| React Native | Not chosen | Official SignalR JS client and mature libraries, but adds TypeScript to mobile without a language I already know; no remaining advantage once the backend is C# |

**Android-first:** Android is assumed to be the most common platform among passengers and staff (to verify), and building for iOS needs a Mac. Logged as an assumption; Flutter keeps iOS possible later.

**Validation (updated 12 Oct):** FCM push is tried on the passenger app day (15 Oct), with **in-app alerts only** as the fallback; cab GPS every 10 s is tried on the crew app day (16 Oct).

**Consequences:** Dart is a new language for me; the live stream uses SSE (ADR-02) to avoid the Flutter SignalR client risk; the cab device is an Android tablet (ADR-05).

## ADR-05 Crew device model: driver phone + cab tablet

**Decision:** one crew app with two modes, chosen by the device it runs on.

- **Phone mode** (driver's own phone): shows today's assigned journeys in order, with train set, time, route and departure platform, so the driver knows which train to walk to. No GPS, no journey actions.
- **Cab mode** (registered Android tablet mounted in the cab, kiosk mode, train-powered): the driver signs in, starts the journey, and the tablet sends GPS positions only while the journey runs.
- **Guard:** *(deferred 14 Oct, domain model D-12; kept as a discovered actor)* would use the same app on a phone for taps and messages and become the backup position source if the tablet fails (A-60). Not built in the MVP.

**Why:** a driver works several journeys a day on different trains, which the phone roster solves. GPS on a personal phone drains its battery, can be killed in the background and makes the driver's location personal data (§20). A full telematics GPS unit needs hardware installation and a vendor integration, which is out of MVP scope.

| Source | Battery / reliability | Privacy | Cost | Result |
| --- | --- | --- | --- | --- |
| Driver's phone GPS | Drains phone; background limits | Personal location data | None | Rejected as primary |
| On-train telematics unit | Best accuracy | Train data | Hardware + vendor integration | Future option |
| **Cab tablet** | Train-powered, always in foreground | Train data during a journey | Low | **Chosen** |

**Rules:**

- The tablet is registered to a train set (seed data); sign-in combines the staff account with the registered device.
- One active cab session per driver; signing in elsewhere ends the previous one (R-19).
- Start journey checks that the journey's assigned train set matches the tablet's; a mismatch warns the driver and alerts the controller (Should, deferred 12 Oct).
- Instructions for the current journey go to the tablet; changes to later journeys go to the phone.
- The driver cannot start a journey from a phone.

**Connectivity:** MVP and initial rollout assume a 4G tablet with a railway business data SIM. Expected use is about 3 MB per day (a position every 10 s for 16 hours), roughly 100 MB per month. Dual-operator SIMs or an onboard router with a roof antenna are future options for weak-coverage sections. Production adds a private APN and mobile device management (kiosk lock, remote wipe, updates).

**App ↔ backend:** HTTPS REST for positions (batched) and actions, with device credential + crew session; the SSE stream for instructions; an offline queue with timestamps and `clientActionId` for tunnels; full journey state reloaded on reconnect. Timestamps use GPS time or a server-calculated clock offset, so a wrong tablet clock cannot make a train look early or late (R-09).

**MVP scope split:** Must: two modes, Start/End with GPS, glance view, taps, messages, one active cab session. Deferred (12 Oct re-plan): staff ID + PIN sign-in, train set mismatch warning, crew incident reports. Could: automatic End journey at the terminus, phone notifications for later journeys.

**Consequences:** device registration and a crew session entity in the model; a new edge case (tablet in the wrong train set); the demo uses an emulator in tablet landscape as the cab device.

## ADR-06 Web front end: React + TypeScript

**Decision:** React with TypeScript, built with Vite as single-page apps. Two separate apps, **ops-web** and **station-display**, in one workspace with shared packages.

**Drivers:**

| Need | Ops web | Displays |
| --- | --- | --- |
| Live updates within 5 s (B-5, SM-3) | Yes | Yes |
| Behaviour when offline | Banner, no queued actions (A-101) | Keep working: cached timetable, trains removed by the clock (US-D4) |
| Runs unattended for days on cheap hardware | – | Yes |
| Dense, keyboard-friendly tables, filters, confirmations (O-2, O-4) | Yes | – |
| Accessibility checks (Q-4) and Playwright E2E (Q-1) | Yes | Yes |

**Options considered:**

| Option | Result | Reason |
| --- | --- | --- |
| Blazor Server | Eliminated for displays | UI runs on the server over a live connection; when the network drops, the screen freezes |
| Next.js | Eliminated | Server rendering is not needed (no SEO, staff sign-in, kiosk screens); adds a second server |
| Flutter web | Eliminated | Canvas rendering is weak for dense tables and keyboard use, and accessibility tools cannot inspect it well |
| Angular / Vue | Not chosen | Capable, but nothing in the requirements favours them; not in training scope |
| Blazor WebAssembly | Viable, not chosen | C# shared with the backend, but smaller component ecosystem and heavier first load |
| **React** | **Chosen** | Largest ecosystem for dense operational UIs, in training scope (§24), strong AI code generation |

**Stack:** TanStack Query (server data, refetch, reload on reconnect) · React Router · a component library for ops-web (Mantine or MUI) · plain CSS with design tokens for displays · React Hook Form · vite-plugin-pwa for the display offline cache · `openapi-typescript` + `openapi-fetch` for typed API calls (ADR-09) · a fetch-based SSE client (ADR-02).

**Two apps, not one:** display devices are public and read-only. They must never download operational UI code or routes, so ops-web and station-display are separate builds (§17).

```
web/
  apps/ops-web           staff only, signed in
  apps/station-display   public screens, read-only
  packages/ui            shared components (status badge, train row, line diagram)
  packages/api-client    types generated from the backend OpenAPI spec
  packages/tokens        design tokens → CSS variables
```

**Consequences:** a third language (C#, Dart, TypeScript); status wording must stay identical across three front ends, handled by generated types, shared tokens and shared status fixtures (Cross-cutting).

## ADR-07 Catering provider: a separate service

**Decision:** the simulated provider (fictional RailBites Catering) is a small, independent ASP.NET Core service with its own PostgreSQL database, its own credentials and an event log page. This resolves open decision A-93 (independent app vs mock) in favour of an independent app.

**Drivers:** the provider must remain a separate external system (§12); signed, retried, de-duplicated events (F-2); provider supplies stations, menus, availability, hotline and hours (F-3); its health shown on the ops dashboard (F-4); events visible for the demo (F-1).

| Option | Result | Reason |
| --- | --- | --- |
| Mock inside SmartRail | Rejected | Hides the boundary the brief asks to show; signatures, retries and outages cannot be demonstrated |
| **Independent service** | **Chosen** | Real network boundary: can be stopped to demonstrate retries and "Menu unavailable"; its own data proves the ownership split |

**Integration contract** (details: API contract §3.6, domain model §8.3):

- SmartRail → provider: ETA changes (≥ 1 min), cancellations, early terminations, skipped / added stops for catering stations only; HMAC-signed over timestamp + body, each with an event ID; timestamps older than 5 min rejected; sent from the outbox with increasing retry intervals.
- Provider → SmartRail: stations, menus, availability, hotline and opening hours, fetched every 5 minutes and cached; journeys calling at its stations for the day.
- No passenger data is shared in this release.

**Consequences:** two services and two databases to run locally (Docker Compose); contract tests protect the boundary. Using the same technology as SmartRail is a convenience for the simulation only; a real provider would use its own stack.

## ADR-08 Testing approach: spec-driven features, test-first rules

**Decision (7 Oct):** a hybrid. **Features are spec-driven** (user stories, acceptance criteria and the API contract define behaviour before code). **Domain rules are built test-first** (TDD) from test names I approve before any code is written. **UI is tested after it is built**, focusing on status states. **Five end-to-end flows** are automated from the ★ acceptance criteria.

**Drivers:** reliability is quality goal #2 (architecture Q1); time-dependent rules (delay, ETA, lead time, staleness) are where AI-generated logic is most likely to be subtly wrong; the build window is short; §18 asks me to justify which tests give the most value; §23 requires 5 automated E2E flows.

| Option | Result | Reason |
| --- | --- | --- |
| Spec-driven only | Rejected | Code can read like the spec without behaving like it; nothing proves the rules |
| TDD everywhere | Rejected | Test-first UI is rewritten constantly while layouts change; doesn't fit the build window |
| **Hybrid** | **Chosen** | Strict tests where bugs mislead passengers and staff; lighter testing where bugs are visible and cheap to fix |

**How it works per story:** spec → contract → test names (AI drafts, **I approve**) → coding agent implements until tests pass → I review the diff → AI usage log entry.

| Area | Approach | Tool |
| --- | --- | --- |
| Domain rules R-01–R-22 (delay, ETA, detection, source priority, lead time, conflicts, control version, scope) | **Test-first**, 48 approved test names (domain model §10) | xUnit + `FakeTimeProvider` |
| API endpoints, outbox, permission matrix, idempotency | Integration tests with each endpoint | xUnit + Testcontainers (PostgreSQL) |
| Module boundaries, domain purity | Architecture tests | NetArchTest |
| Status wording in three languages | Shared fixtures (`shared/status-fixtures.json`) run in every app | xUnit · Vitest · Flutter test |
| Screens | Component / widget tests after build | Vitest + RTL · Flutter widget tests |
| 5 critical flows | Automated E2E | Playwright (+ Flutter integration tests, QA strategy K-2) |
| Accessibility | Scans + manual screen-reader check | axe, TalkBack |

**Consequences:** I review test names before code every time; tests written by an AI are themselves reviewed for weak assertions (optional mutation testing on the domain core). Details: `qa/strategy.md`.

## ADR-09 API contract and client types

**Decision (8–12 Oct):** **design-first, code-generated.** The API is designed in `develop/02-api-contract.md` (endpoints, auth per endpoint, error codes, stream) and reviewed before implementation. The machine-readable `backend/openapi.yaml` is **generated from the ASP.NET Core code**, committed, and checked in CI; any difference from the design document is resolved in the document first.

**Client types (C-8, 12 Oct):** TypeScript types are generated with `openapi-typescript` and called with `openapi-fetch`. Dart uses a generator only if its output is clean and readable; otherwise hand-written models plus contract tests that parse the examples in `openapi.yaml`. SSE events (not covered by OpenAPI) each have one example JSON in `shared/events/`, parsed by tests in every app.

**Drivers:** the same data shapes exist in C#, TypeScript and Dart; a renamed field must fail at build or test time, not in the demo (reliability); agentic development needs one written source of truth; .NET server-stub generators are clumsy.

| Option | Result | Reason |
| --- | --- | --- |
| Contract-first (hand-written `openapi.yaml`, server built to match) | Rejected | Long YAML to write and keep correct by hand; .NET stub generation is awkward; tends to drift |
| Code-only (no design document) | Rejected | The agent would invent the API; nothing for me to review before code exists |
| **Design document + generated spec + generated client types** | **Chosen** | I design and review the API; machines keep the three languages in step |

**Consequences:** a small CI step (regenerate and diff); one hour of setup for TypeScript generation; Dart decided on the passenger app day. The contract is also written into `CLAUDE.md` so the coding agent follows it.

## ADR-10 Authentication: ASP.NET Core Identity + JWT, no full OAuth 2.0 in the MVP

**Decision (12 Oct):** ASP.NET Core Identity provides the user store, password hashing and lockout. The backend issues **JWT access tokens (15 min)** and **rotating refresh tokens** (passengers: 30-day sliding; staff: 30-min idle; crew: shift-length; cab: bound to device + session). Authorisation uses ASP.NET Core policies (`Controller`, `StationManager`, `CrewPhone`, `CrewCab`, `Display`, `Provider`, `Passenger`) plus scope checks in handlers.

**Drivers:** separate credential types for passengers, staff, devices and the provider (B-7); passengers stay signed in (US-P5); staff sessions expire after 30 min idle (US-O1); public display devices must be read-only (Q-2); explainable in review.

| Option | Result | Reason |
| --- | --- | --- |
| Full OAuth 2.0 / OpenID Connect with an authorisation server (Keycloak, OpenIddict, Entra ID) | Not in MVP | All four apps are first-party and call only SmartRail's API, so there is no third party to delegate access to; adds a server, redirect flows and about a day of setup |
| Hand-written authentication | Rejected | Password hashing and lockout are security-critical and must not be custom |
| **Identity + backend-issued JWT** | **Chosen** | Uses the framework for the risky parts; tokens follow OAuth practices (short-lived bearer tokens, rotation, reuse detection, claims) |

**Browser token storage (C-3):** ops-web keeps the access token in memory and the refresh token in an `HttpOnly; Secure; SameSite=Strict` cookie, so injected scripts cannot read a long-lived token.

**Consequences / upgrade path:** OIDC when staff sign in with the railway's directory (single sign-on, D-10); OAuth client credentials when partners call the API; because tokens already follow OAuth conventions, the change replaces the token issuer, not the apps or rules. Staff accounts are provisioned by the organisation (seed data in the MVP), never self-registered.

## ADR-11 Backend code structure

**Decision (12 Oct):** **feature slices** in every module (one folder per feature: endpoint, handler, validation, tests). Modules with real business rules (**LiveOperations, Disruptions**) also get a separate **pure `*.Domain` project** (aggregates, value objects, rules), which references no EF Core, ASP.NET Core, HTTP or logging packages and never reads the clock. Simple modules (Network, Identity, Notifications, CateringIntegration) have no domain project. Handlers use EF Core directly; no repository layer, no mediator library.

**Drivers:** the six modules differ greatly in complexity; rules must stay pure for fast TDD; a coding agent must get immediate feedback if it leaks infrastructure into rules; explainability and the build window.

| Option | Result | Reason |
| --- | --- | --- |
| Full Clean Architecture (Domain / Application / Infrastructure / API per module) | Rejected | ~24 projects; 4 files per feature; repositories wrapping EF Core; four layers even for a read-only station lookup |
| Feature slices with a `Domain/` folder | Viable, not chosen | Purity only by convention; an agent can add EF Core code there and only a later test catches it |
| **Feature slices + pure Domain projects where rules live** | **Chosen** | Keeps Clean Architecture's dependency rule where it matters, enforced by the compiler; everything else stays simple |

**Consequences:** 8 module projects + host + simulator instead of ~24; the build fails if a Domain project gains an infrastructure reference; NetArchTest additionally enforces module boundaries. Full reasoning: `develop/03-architecture-principles.md` Q6.

## ADR-12 Mobile architecture (passenger app first, driver app later)

**Decision (17 Oct, Senuja):** the Flutter apps use only what the SDK and one official package give us. Every point below has a one-line reason.

| # | Decision | Reason |
| --- | --- | --- |
| 1 | **State:** `ChangeNotifier` / `ValueNotifier` with constructor injection; no state-management package. A screen's state is a Dart 3 `sealed class` (loading / live / stale / error) shown with a `switch`; no `freezed`. | Built into Flutter; a missing case is a compile error; nothing to learn or explain beyond the SDK. |
| 2 | **Navigation:** the built-in `Navigator`; no routing package. | The passenger app has a handful of screens and no deep links in the MVP. |
| 3 | **HTTP and SSE:** the official `http` package (publisher dart.dev). The stream is read from `Client.send()`'s streamed response and parsed in `packages/core`; no SSE package. | ADR-02: SSE is plain HTTP; the parser is about 40 lines we can test with the files in `shared/events/`. |
| 4 | **Models:** hand-written plain Dart classes with hand-written `fromJson` in `packages/core`, only for what the passenger screens use. **No code generation of any kind** (no OpenAPI generator, `json_serializable`, `freezed`, `build_runner`). | Narrows ADR-09 for Dart ("generator only if clean, else hand-written models + contract tests"): decided on the passenger app day, hand-written. Every line is readable in a review. |
| 5 | **Structure:** `mobile/packages/core` (api, models, stream, theme, status texts, time formatting) is shared with the driver app; `mobile/apps/passenger/lib/features/<screen>/` holds one folder per screen. | Same idea as the backend's feature slices; the driver app reuses core without copying. |
| 6 | **Times:** the server sends UTC; the app always shows Asia/Colombo (UTC+05:30, no daylight saving) through **one tested function in core**. Device local time is never used. | The Android emulator runs in UTC and would show every train 5½ hours off. A fixed offset needs no timezone package. |
| 7 | **API base URL:** `--dart-define=API_BASE_URL`, default `http://10.0.2.2:5045`. | `10.0.2.2` is how the Android emulator reaches the PC's localhost. |
| 8 | **No persistent cache yet.** Data lives in memory only and always carries an age label. | Simplest thing that meets US-P4; ADR-03 lists SQLite / key-value for the passenger app, which stays deferred until a story needs it (offline start, saved trips). |
| 9 | **Tests:** unit tests (models and contract, status texts from `status-fixtures.json`, time formatting, age labels) and widget tests against a fake backend (`MockClient` from the `http` package, like ops-web's fakes). Tests are written in the same commit as the code. | CLAUDE.md §5.6 and ADR-08. |
| 10 | **UI:** Material 3 (built into Flutter). The `ColorScheme` is written out from `shared/tokens.json` for light and dark; `ColorScheme.fromSeed` is not used. Status colours and icons live in a `ThemeExtension` in `packages/core`. Text is Noto Sans / Noto Sans Mono (tabular figures) at the passenger token sizes, bundled as font assets with the OFL licence (no `google_fonts`). Icons are built-in Material icons mapped from the token icon names. Minimum touch target 48 px. | `fromSeed` would replace the token colours and their checked contrast (design handoff rules 11–12). Bundled fonts work offline and need no package. |
| 11 | **Token values in Dart:** `core/lib/src/theme/tokens.dart` is a hand-written copy of `tokens.json`, and a test reads the JSON and fails on any difference. | An asset cannot live outside the package, and code generation is ruled out by point 4; the test makes drift impossible to miss. |
| 12 | **Age of live data:** at receipt, age = HTTP `Date` header − `updatedAt`; after that the app counts with a `Stopwatch`. Without a `Date` header no age is shown. A stream event carrying a new `updatedAt` resets the age to 0 on receipt. The app applies no stale / lost thresholds: it shows `freshness.tracking` as sent. | Q4: the phone's clock is never compared with the server's; the age is measured by the app's own stopwatch. |
| 13 | **Live updates on the details screen (18 Oct):** `GET /stream?journeyIds=<id>` is read while the screen is open; the first frame is a snapshot, and after a break the app resumes with `Last-Event-ID`. A `TrainPositionUpdated` is applied directly (marker, and the age starts at zero); every other event, known or not, and the snapshot, make the journey load again (debounced by 500 ms), and a `resync` loads it at once. Frames with a sequence not above the last one seen are ignored. No bytes for 45 s (the heartbeat comes every 15 s) means the connection is lost: reconnect after 1, 2, 4, 8, 16, then every 30 s, and meanwhile `GET /journeys/{id}` every 30 s (ADR-02 fallback). The stream stops when the screen closes. | The app never applies business changes itself (CLAUDE.md §3): only a position is raw data, everything else is re-read from the backend, which also covers events the app does not know yet. The SSE parser and the silence timer are written by hand in `packages/core` (point 3). |

**Colours and icons that are not in `tokens.json` (approved 18 Oct):** the live-data dot uses the accent colour (not the "On time" green, which would mix meanings next to "8 min late"); a skipped stop ("Does not stop") uses the Cancelled colours with the Material `block` icon; an added stop ("Extra stop") and "Completed" use the neutral Scheduled style; the train marker, `restaurant` (food), `cloud_off` (offline) and `search_off` / `error_outline` (empty and error states) are plain Material icons without a token name. Non-status wording ("Your station", "destination", "Food available at X", error and search texts) lives in one file, `packages/core/lib/src/strings.dart`; status wording only in `status-fixtures.json`.

**What the app never does (CLAUDE.md §3, Q4):** calculate delay, ETA or status. It shows what the backend sends, formatted by the shared fixtures, plus an age label counted by the app's own stopwatch (S-17), never by comparing the phone clock with a server time.

**Contract tests:** one test parses every file in `shared/events/` and every journey / station example used by the fake backend into the Dart models; a missing or renamed field must fail a test. Parsing is strict about fields the screens need (a missing one throws) and ignores unknown fields (API contract §1).

**Consequences:** more typing than generated models, paid back by readable code and no build step. The Dart models can drift from the backend; the contract tests and the regenerated `openapi.yaml` are the guard. Android only needs cleartext HTTP in **debug** builds to reach `10.0.2.2` (release builds use HTTPS).

## Cross-cutting decisions

**Consistency across three languages (A-99):** the same status must read the same everywhere ("3 min late", "1 h 15 min late", "Platform 5 (was 2)").

1. **Generated types:** TypeScript (and, if clean, Dart) API types are generated from `openapi.yaml`, so data shapes and enums have one source (ADR-09).
2. **Shared design tokens:** colours, type sizes and status icons live in `shared/tokens.json`, generated into CSS variables and Dart constants. Every status is shown with icon + text + colour; all colour pairs meet WCAG AA.
3. **Shared status fixtures:** `shared/status-fixtures.json` (input → expected text), run in the backend, TypeScript and Flutter test suites; any app that formats differently fails its tests.
4. **Shared event examples:** `shared/events/*.json`, one per SSE event type.

**Security built into the stack (§17):** ASP.NET Core policy authorisation for role + scope; separate credential types for passengers, staff, devices and the provider; rate-limiting middleware; HMAC signatures on provider events; display app built separately with read-only device credentials; announcements rendered as plain text; dependency and secret scanning in the build pipeline. Threats and controls: `security/threat-model.md`.

**Observability (§19):** structured logging with request IDs and no personal data; health checks per service; OpenTelemetry for traces and metrics; display heartbeats.

**Local environment:** Docker Compose runs PostgreSQL, the SmartRail backend and the provider service; the position simulator calls the same API as the cab tablet.

**Agentic development:** the coding agent (Claude Code) works from `CLAUDE.md` at the repository root, which records these decisions, hard architecture and security rules, the definition of done and a "stop and ask before…" list.

## Validation

The spikes planned for 8–9 Oct were replaced on 12 Oct by build steps that test the same risks, each with a fallback:

| Risk | Validated by | Fallback |
| --- | --- | --- |
| SSE reaches browser and Flutter in < 5 s and recovers after reconnect | Walking skeleton, 13 Oct | SignalR (web), polling (Flutter) |
| FCM push arrives with the app closed | Passenger app day, 15 Oct | In-app alerts only |
| Cab GPS every 10 s with screen kept on | Crew app day, 16 Oct | Simulator for the demo |
| Dart client generation is clean | Passenger app day, 15 Oct | Hand-written models + contract tests |

## Documents updated by these decisions

| Document | Change | Status |
| --- | --- | --- |
| Assumptions log | A-29 (location belongs to the train), A-54, A-57 (dedicated cab tablet, kiosk), A-60 (tablet primary, guard's phone backup), A-93 (independent provider service); new: Android-first, cab tablet 4G connectivity, GPS / server clock source, one line + one controller, staff provisioning | To do (my repo) |
| User stories | US-C1, US-C2, US-B3 AC1 | Done |
| Edge cases | Tablet in the wrong train set; wrong tablet clock; driver forgets to end the journey | Done |
| Domain model | CabDevice, CrewSession, control version | Done |
| MoSCoW | Crew app split; one line / one controller (9 Oct); re-plan cuts (12 Oct) | Done |

## AI usage log

| Date | Activity | AI suggestion | My decision | Why |
| --- | --- | --- | --- | --- |
| 7 Oct | Backend | Leaned to Node.js (training coverage, shared TypeScript); eliminated Next.js | **Overrode:** ASP.NET Core | C# fluency; built-in features map to specific requirements |
| 7 Oct | Mobile | Flagged the Flutter SignalR client as a risk; checked package health | Accepted Flutter; adopted SSE as default transport | Evidence from package data, not the first suggestion |
| 7 Oct | Position source | Driver phone GPS, then an on-train telematics unit | **Own proposal:** cab tablet + phone roster | Balances cost, privacy and reliability; covers several journeys per day |
| 7 Oct | Web | Recommended Blazor WebAssembly (C# fluency) | **Overrode:** React | Ecosystem, training scope and AI support outweigh a third language |
| 7 Oct | Web | Eliminated Blazor Server for displays; two separate web apps | Accepted | Offline displays; public devices must not load ops code |
| 7 Oct | Database | PostgreSQL; rejected MongoDB; deferred Redis and PostGIS | Accepted | Relational domain; avoid unneeded infrastructure in the MVP |
| 7 Oct | Testing | My question: TDD or spec-driven? AI proposed a hybrid | Accepted (ADR-08) | Matches where AI-written code is riskiest |
| 8–12 Oct | API contract | Contract-first YAML vs design doc + generated spec | **Chose** design doc + generated spec (ADR-09); questioned code generation before accepting it (C-8) | One source of truth; I review the design, machines keep languages in step |
| 12 Oct | Authentication | My question: do we need OAuth 2.0? | **Not in MVP** (ADR-10) | First-party clients only; upgrade path kept |
| 12 Oct | Code structure | Proposed feature slices with a domain folder; I asked "why not Clean Architecture?" | **Chose** slices + compiler-enforced Domain projects for rule-heavy modules (ADR-11) | Discussion showed modules differ in complexity; compiler feedback suits a coding agent |
