# SmartRail – Technology Stack Decisions

Oct 7, 2026 · @Senuja Bodhinayake

## Summary

SmartRail runs on an ASP.NET Core (C#) backend with PostgreSQL, Flutter for the passenger and crew apps, and React for the operations web app and station displays. The catering provider is a separate service with its own database. Every choice below is traced to requirement IDs in `define/moscow.md`, `define/user-stories.md` and the assumptions log.

**Decision method (same for every layer):**

1. List the drivers: requirements that constrain the technology (with IDs).
2. Eliminate options that fail a hard constraint.
3. Compare the remaining options on the drivers, including my own skills and the 7-day build window (10–16 Oct).
4. Validate the riskiest assumption with a short spike before the build starts.
5. Record the decision, alternatives and consequences as an ADR, and log where AI suggestions were accepted or overridden.

| Layer | Choice | Main reason | Status |
| --- | --- | --- | --- |
| Backend | ASP.NET Core (C#), modular monolith | Built-in real-time, background jobs, policy authorisation and `TimeProvider`; my C# fluency | Decided |
| Live update transport | Server-Sent Events (default), SignalR as alternative | One-way stream fits the event model; no fragile Flutter client library | Pending spike (8–9 Oct) |
| Database | PostgreSQL + EF Core | Relational domain, transactions, outbox, free at national scale | Decided |
| Mobile | Flutter, Android-first | Mature push and location plugins; in training scope | Decided |
| Crew device model | Driver phone (roster) + registered cab tablet (operate, GPS) | No GPS on personal phones; train-powered, kiosk device | Decided |
| Web front end | React + TypeScript (Vite), two apps | Largest ecosystem, in training scope, strong AI support | Decided |
| Catering provider | Separate ASP.NET Core service + own database | Brief §12 requires a separate external system | Decided |
| Testing | xUnit + Testcontainers, Vitest, Flutter tests, Playwright + axe | One tool per layer; E2E across apps | Decided |

&#91;embedded content: solution architecture · 4 clients, 1 backend, 2 databases\]

The catering provider is reached only through its API and signed events; SmartRail never reads the provider's database.

## ADR-01 Backend: ASP.NET Core (C#)

**Decision:** ASP.NET Core (current LTS) as a modular monolith: one deployable application with internal modules for Network, Live Operations, Disruptions, Identity, Notifications and Catering Integration.

**Drivers:**

- One event stream pushed to every app within 5 s (B-5, SM-3).
- Always-running background work: stale / lost GPS timers (A-26), unscheduled-stop alert after 3 min (O-8), daily journey generation (B-2), provider event retries (F-2).
- Role + scope authorisation on every request (O-1, B-7, E-20).
- Controllable clock for tests (A-106).
- Concurrent edits refused (A-66), rate limits (US-P5, Q-2), structured logs and health checks (Q-3).
- Must be explainable and modifiable live during review (§28) within a 7-day build.

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
| Timers, generation, retries (A-26, O-8, B-2, F-2) | `BackgroundService` / hosted services |
| Role + scope (O-1, B-7) | Policy-based and resource-based authorisation |
| Controllable clock (A-106) | `TimeProvider`, `FakeTimeProvider` in tests |
| Validation and errors | Model validation + ProblemDetails |
| Rate limits (US-P5) | Rate-limiting middleware |
| Concurrency (A-66) | EF Core concurrency tokens |
| Logs, health (Q-3) | `ILogger` structured logging, health checks, OpenTelemetry |

**Why a modular monolith, not microservices:** one engineer, one week. Module boundaries keep the code maintainable; a single deployable keeps transactions, debugging and the demo simple. Modules can be split later where load requires it.

**Consequences:**

- No shared types with the TypeScript and Dart front ends; mitigated by generating clients from the OpenAPI spec (see Cross-cutting).
- .NET is not in the training list (§24); the brief allows free choice, and the reasons above justify it.
- Scaling to several instances later needs a SignalR backplane or shared event bus (Redis); not needed for the MVP.

## ADR-02 Live update transport: SSE by default

**Decision (pending spike):** push live events from server to clients with Server-Sent Events (SSE). All client actions (cancel, Arrived, incident report) stay normal REST calls. SignalR remains the alternative if the spike shows a reason to prefer it.

**Drivers:** one event stream to passenger app, crew app, ops web and displays (B-5, A-73); action → every client in < 5 s (SM-3); full state reload on reconnect (A-102); displays keep working offline (US-D4).

**Why SSE fits:** the event stream is one-way (server → clients). SSE is plain HTTP, so browsers read it with the built-in `EventSource` and Flutter reads it as a streamed HTTP response. No special client library is needed on any of the three front-end technologies.

| Option | Strengths | Weaknesses |
| --- | --- | --- |
| **SSE** | Plain HTTP; no client library; native browser support; simple to test | Server → client only (fine here); groups and reconnect logic written by me |
| SignalR | Groups, automatic reconnect, scale-out backplane; official .NET and JavaScript clients | No official Flutter client: community packages are fragmented into several forks, with weak maintenance scores and known callback issues ([depscope](https://mcp.depscope.dev/pkg/pub/signalr_netcore), [pub.dev](https://pub.dev/documentation/signalr_netcore3/latest/)) |

**What I build myself with SSE:** a filter per client (station, journey, region), a sequence number per event, and "reconnect → fetch snapshot → resume stream", which A-102 requires anyway.

**Consequences:** less framework help with connection groups; scale-out later needs a shared event bus (e.g. Redis pub/sub) behind the SSE endpoints.

## ADR-03 Database: PostgreSQL + EF Core

**Decision:** PostgreSQL accessed through EF Core (Npgsql), running in Docker locally and in tests.

**Drivers:**

| Requirement | Database implication |
| --- | --- |
| Station → route → service → scheduled stop → journey → stop status; crew and train set assignments (S4 model) | Relational, with foreign keys and constraints |
| A controller action updates journey, stop statuses and events together (O-4) | Transactions across tables |
| 100% of events reach the provider, with retries (F-2, SM-5) | Transactional outbox in the same database |
| Concurrent edits refused (A-66) | Optimistic concurrency (row version) |
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

- **Redis:** one backend instance keeps running journeys' live state in memory and persists changes. Added later as an event bus / backplane for scale-out.
- **PostGIS:** station zones (300 m) and off-route checks (500 m) are simple distance maths in C#, easier to unit-test with the controllable clock. PostGIS is the upgrade path.

**All data stores in the solution:**

| Where | Store | Holds |
| --- | --- | --- |
| SmartRail backend | PostgreSQL, database `smartrail` | Network, timetable, journeys, live status, accounts, audit log, positions, outbox |
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
| .NET MAUI | Viable, not chosen | One language with the backend and an official SignalR client; actively developed by Microsoft ([issoh.co.jp](https://www.issoh.co.jp/tech/details/7938/)), but push and background location rely on community plugins or custom Android code, a schedule risk in a 7-day build |
| React Native | Not chosen | Official SignalR JS client and mature libraries, but adds TypeScript to mobile without a language I already know; no remaining advantage once the backend is C# |

**Android-first:** Android is assumed to be the most common platform among passengers and staff (to verify), and building for iOS needs a Mac. Logged as an assumption; Flutter keeps iOS possible later.

**Consequences:** Dart is a new language for me; the live stream uses SSE (ADR-02) to avoid the Flutter SignalR client risk; the cab device is an Android tablet (ADR-05).

## ADR-05 Crew device model: driver phone + cab tablet

**Decision:** one crew app with two modes, chosen by the device it runs on.

- **Phone mode** (driver's own phone): shows today's assigned journeys in order, with train set, time, route and departure platform, so the driver knows which train to walk to. No GPS, no journey actions.
- **Cab mode** (registered Android tablet mounted in the cab, kiosk mode, train-powered): the driver signs in, starts the journey, and the tablet sends GPS positions only while the journey runs.
- **Guard:** uses the same app on a phone for taps, incident reports and messages; becomes the backup position source if the tablet fails (A-60).

**Why:** a driver works several journeys a day on different trains, which the phone roster solves. GPS on a personal phone drains its battery, can be killed in the background and makes the driver's location personal data (§20). A full telematics GPS unit needs hardware installation and a vendor integration, which is out of MVP scope.

| Source | Battery / reliability | Privacy | Cost | Result |
| --- | --- | --- | --- | --- |
| Driver's phone GPS | Drains phone; background limits | Personal location data | None | Rejected as primary |
| On-train telematics unit | Best accuracy | Train data | Hardware + vendor integration | Future option |
| **Cab tablet** | Train-powered, always in foreground | Train data during a journey | Low | **Chosen** |

**Rules:**

- The tablet is registered to a train set (seed data); sign-in combines the staff account with the registered device.
- One active cab session per driver; signing in elsewhere ends the previous one.
- Start journey checks that the journey's assigned train set matches the tablet's; a mismatch warns the driver and alerts the controller (Should).
- Instructions for the current journey go to the tablet; changes to later journeys go to the phone.
- The driver cannot start a journey from a phone.

**Connectivity:** MVP and initial rollout assume a 4G tablet with a railway business data SIM. Expected use is about 3 MB per day (a position every 10 s for 16 hours), roughly 100 MB per month. Dual-operator SIMs or an onboard router with a roof antenna are future options for weak-coverage sections. Production adds a private APN and mobile device management (kiosk lock, remote wipe, updates).

**App ↔ backend:** HTTPS REST for positions (batched) and actions, with device credential + crew session; the SSE stream for instructions; an offline queue with timestamps for tunnels; full journey state reloaded on reconnect. Timestamps use GPS time or a server-calculated clock offset, so a wrong tablet clock cannot make a train look early or late.

**MVP scope split:** Must: two modes, Start/End with GPS, glance view, taps, messages, one active cab session. Should: staff ID + PIN sign-in, train set mismatch warning. Could: automatic End journey at the terminus, phone notifications for later journeys.

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
| Text-to-speech (S-1) | – | Yes |
| Accessibility checks (Q-4) and Playwright E2E (Q-1) | Yes | Yes |

**Options considered:**

| Option | Result | Reason |
| --- | --- | --- |
| Blazor Server | Eliminated for displays | UI runs on the server over a live connection; when the network drops, the screen freezes |
| Next.js | Eliminated | Server rendering is not needed (no SEO, staff sign-in, kiosk screens); adds a second server |
| Flutter web | Eliminated | Canvas rendering is weak for dense tables and keyboard use, and accessibility tools cannot inspect it well |
| Angular / Vue | Not chosen | Capable, but nothing in the requirements favours them; not in training scope |
| Blazor WebAssembly | Viable, not chosen | C# shared with the backend, but smaller component ecosystem and heavier first load |
| **React** | **Chosen** | Largest ecosystem for dense operational UIs, in training scope (§24), official SignalR JS client and native `EventSource`, strong AI code generation |

**Stack:** TanStack Query (server data, refetch, reload on reconnect) · React Router · a component library for ops-web (Mantine or MUI) · plain CSS with design tokens for displays · React Hook Form · vite-plugin-pwa for the display offline cache · browser Web Speech API for announcements.

**Two apps, not one:** display devices are public and read-only. They must never download operational UI code or routes, so ops-web and station-display are separate builds (§17).

```
web/
  apps/ops-web           staff only, signed in
  apps/station-display   public screens, read-only
  packages/ui            shared components (status badge, train row, line diagram)
  packages/api-client    generated from the backend OpenAPI spec
  packages/tokens        design tokens → CSS variables
```

**Consequences:** a third language (C#, Dart, TypeScript); status wording must stay identical across three front ends, handled by generated clients, shared tokens and shared test fixtures (Cross-cutting).

## ADR-07 Catering provider: a separate service

**Decision:** the simulated provider (fictional RailBites Catering) is a small, independent ASP.NET Core service with its own PostgreSQL database, its own credentials and an event log page. This resolves open decision A-93 (independent app vs mock) in favour of an independent app.

**Drivers:** the provider must remain a separate external system (§12); signed, retried, de-duplicated events (F-2); provider supplies stations, menus, availability, hotline and hours (F-3); its health shown on the ops dashboard (F-4); events visible for the demo (F-1).

| Option | Result | Reason |
| --- | --- | --- |
| Mock inside SmartRail | Rejected | Hides the boundary the brief asks to show; signatures, retries and outages cannot be demonstrated |
| **Independent service** | **Chosen** | Real network boundary: can be stopped to demonstrate retries and "Menu unavailable"; its own data proves the ownership split |

**Integration contract:**

- SmartRail → provider: ETA changes (≥ 1 min), cancellations, early terminations, skipped / added stops for catering stations only; HMAC-signed, each with an event ID; sent from the outbox with increasing retry intervals.
- Provider → SmartRail: stations, menus, availability, hotline and opening hours, fetched every 5 minutes and cached; journeys calling at its stations for the day.
- No passenger data is shared in this release.

**Consequences:** two services and two databases to run locally (Docker Compose); contract tests protect the boundary. Using the same technology as SmartRail is a convenience for the simulation only; a real provider would use its own stack.

## Cross-cutting decisions

**Consistency across three languages (A-99):** the same status must read the same everywhere ("3 min late", "Platform 5 (was 2)").

1. **Generated clients:** TypeScript and Dart API clients are generated from the backend's OpenAPI spec, so data shapes and enums have one source.
2. **Shared design tokens:** colours, type sizes and status icons live in one JSON file, generated into CSS variables and Dart constants.
3. **Shared status test fixtures:** one JSON file of input → expected text, run in both the TypeScript and Flutter test suites; any app that formats differently fails its tests.

**Testing stack (§18):**

| Layer | Tool | Covers |
| --- | --- | --- |
| Backend rules | xUnit + `FakeTimeProvider` | ETA, delay, punctuality, staleness, permissions |
| Backend integration | xUnit + Testcontainers (PostgreSQL) | APIs, outbox, concurrency, authorisation matrix |
| Provider contract | Contract tests | Event schema, signatures, duplicates |
| Web components | Vitest + React Testing Library | Status rendering, offline banner |
| Mobile | Flutter widget and integration tests | Modes, offline queue, status rendering |
| End to end | Playwright + axe | The 5 critical flows (Q-1), accessibility checks (Q-4) |

**Security built into the stack (§17):** ASP.NET Core policy authorisation for role + scope; separate credential types for passengers, staff, devices and the provider; rate-limiting middleware; HMAC signatures on provider events; display app built separately with read-only device credentials; announcements rendered as plain text; dependency and secret scanning in the build pipeline.

**Observability (§19):** structured logging with request IDs and no personal data; health checks per service; OpenTelemetry for traces and metrics; display and terminal heartbeats.

**Local environment:** Docker Compose runs PostgreSQL, the SmartRail backend and the provider service; the position simulator calls the same API as the cab tablet.

## Validation, document updates and AI log

**Spikes before the build (8–9 Oct):**

- [ ] Backend: POST a position → detect arrival → recalculate delay → event reaches a browser over SSE in under 5 s.
- [ ] Flutter on a real Android device: location in the cab app posts to the backend every 10 s with the screen kept on.
- [ ] Flutter: the live stream (SSE) updates a screen within 5 s and recovers after the network is switched off and on.
- [ ] Flutter: one FCM push arrives with the app closed.
- [ ] Decide SSE vs SignalR (ADR-02) from the results.

**Documents to update:**

| Document | Change |
| --- | --- |
| Assumptions log | A-29 (location belongs to the train), A-54, A-57 (dedicated cab tablet, kiosk), A-60 (tablet primary, guard's phone backup), A-93 (independent provider service); new: Android-first, cab tablet 4G connectivity, GPS / server clock source |
| User stories | US-C1 (phone roster + cab sign-in), US-C2 (start only on the cab device, train set check), US-B3 AC1 (positions from a registered cab device in a started journey) |
| Edge cases | Tablet in the wrong train set; wrong tablet clock; driver forgets to end the journey |
| Domain model | CabDevice (bound to a train set), CrewSession (driver + device + start / end) |
| MoSCoW | Crew app Must / Should / Could split from ADR-05 |

**AI usage log entries (7 Oct):**

| Activity | AI suggestion | My decision | Why |
| --- | --- | --- | --- |
| Backend | Leaned to Node.js (training coverage, shared TypeScript); eliminated Next.js | **Overrode:** ASP.NET Core | C# fluency; built-in features map to specific requirements |
| Mobile | Flagged the Flutter SignalR client as a risk; checked package health | Accepted Flutter; adopted SSE as default transport | Evidence from package data, not the first suggestion |
| Position source | Driver phone GPS, then an on-train telematics unit | **Own proposal:** cab tablet + phone roster | Balances cost, privacy and reliability; covers several journeys per day |
| Web | Recommended Blazor WebAssembly (C# fluency) | **Overrode:** React | Ecosystem, training scope and AI support outweigh a third language |
| Web | Eliminated Blazor Server for displays; two separate web apps | Accepted | Offline displays; public devices must not load ops code |
| Database | PostgreSQL; rejected MongoDB; deferred Redis and PostGIS | Accepted | Relational domain; avoid unneeded infrastructure in the MVP |
