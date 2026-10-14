# SmartRail – Backend Code Guide

**Written for:** Senuja, to read before a review (§28) and to explain the code without the AI.
**Kept up to date by:** the coding agent, after every change to backend code (CLAUDE.md §5, step 9).
**Last updated:** 14 Oct 2026 · **Covers:** solution scaffold, Network module (model, migration, seed, `GET /stations`, public schedule interface), LiveOperations (daily journey generation, US-B2), Api host, API description (`openapi.yaml`), tests.

For *what* the system must do, read the specs (`docs/define/`, `01-domain-model.md`, `02-api-contract.md`). This guide explains *how the code does it* and *why it is written that way*.

---

## 1. The big picture

```
 shared/seed/*.csv, *.json  ──►  SeedReader ──► SeedValidator ──► NetworkSeeder ──►  PostgreSQL, schema "network"
 (real + fictional data)         (reads)        (checks rules)     (writes)               ▲
                                                                                         │
 App starts ► NetworkStartupService: migrate (Development only) ► seed ► server accepts requests
                                                                                         │
 Browser / app ► GET /api/v1/stations?query=kan ► SearchStationsEndpoint ► Validator ► Handler ┘
```

One program (the *modular monolith*, ADR-01) made of modules. Each module owns its own database schema and never reads another module's tables (CLAUDE.md §3).

| Project (backend/src) | What it is | State |
|---|---|---|
| `SmartRail.Api` | The host: starts the program, connects the modules, serves HTTP | Network and LiveOperations wired in |
| `SmartRail.Network` | The timetable data: stations, routes, services, train sets | **Built** (this guide) |
| `SmartRail.LiveOperations` / `.Domain` | Today's journeys and the delay/ETA rules | **Daily journey generation built** (section 4); no delay/ETA yet |
| `SmartRail.Disruptions` / `.Domain` | Incidents | Empty |
| `SmartRail.Identity`, `.Notifications`, `.CateringIntegration` | Accounts, push, catering | Empty |
| `SmartRail.Simulator` | Replays GPS through the API | Empty |

---

## 2. The Api host

### `SmartRail.Api/Program.cs`
**What:** the first code that runs. It builds the web application and connects the modules.
**How:**
1. `AddNetworkModule(...)` registers the Network module's database and startup work.
2. `AddOpenApi()` / `MapOpenApi("/openapi/{documentName}.yaml")` make the application describe its own API at `/openapi/v1.yaml` (OpenAPI, as YAML). The committed copy is `backend/openapi.yaml`; see section 5. Two *transformers* adjust the description here, once for every module: one removes the `servers` entry (it would be copied from whichever request produced the file), and one adds `code`, `errors` and `requestId` to the `ProblemDetails` schema, because the framework only knows the standard fields and contract §6 says every error carries them. The error responses themselves are still built where the error happens.
3. `app.MapGroup("/api/v1")` creates the URL prefix every endpoint lives under (contract §1); `MapNetworkEndpoints()` puts the Network endpoints in it.
4. `AddSingleton(TimeProvider.System)` registers the one clock of the whole application; tests replace it with a fixed one. `AddNetworkModule` is called **before** `AddLiveOperationsModule` because hosted services start in the order they are registered and LiveOperations' start-up check needs the network data that Network loads first.
5. `public partial class Program;` at the bottom exists only so the integration tests can start the whole application in memory.

**Why:** the host knows about *all* modules but modules do not know about each other. The URL prefix is decided here, not in the module.
**Watch out:** `MapOpenApi()` currently runs in every environment, so the API description is public in production too. Decide before release whether it should be Development only.

---

## 3. The Network module (`SmartRail.Network`)

Network is **read-only** in the MVP: everything comes from the seed files. It holds the *plan* (what should run), not what is happening today (that is Live Operations).

### 3.1 `NetworkModule.cs` — the module's entry points
**What:** two extension methods the host calls, plus the startup service.

| Piece | What it does |
|---|---|
| `AddNetworkModule` | Registers `NetworkDbContext` with PostgreSQL (`UseNpgsql`). If the connection string `ConnectionStrings:SmartRail` is missing it throws a message that tells you the exact `dotnet user-secrets set` command. The migration history table is placed *inside* the `network` schema, so each module migrates independently. Registers `NetworkSeeder` and the startup service. |
| `MapNetworkEndpoints` | Registers the module's HTTP endpoints into the group the host passes in. |
| `NetworkStartupService` | Runs once at start, **before** the server accepts requests (that is how a hosted service behaves). Development: applies pending migrations automatically. Any other environment: refuses to start if migrations are pending. Then loads the seed. An exception here stops the whole application. |

**Why:** migrations apply automatically only in Development so a production database is never changed by simply starting the program (decision 7, 13 Oct).

### 3.2 `Model/` — the tables as C# classes
Plain classes, one per table, no behaviour (domain model §3). Files: `Stations.cs`, `Routes.cs`, `Timetable.cs`, `TrainSets.cs`.

| Class | Meaning | Key points |
|---|---|---|
| `Station`, `Platform` | A station and its numbered platforms | `Code` ("KDT") is the unique natural key. Platform `Number` is text. |
| `Line`, `Route`, `RouteStation` | The line, one route per direction, and the ordered stations with distance | `Direction`: **Up = Fort→Kandy, Down = Kandy→Fort** |
| `Service`, `ScheduledStop` | A timetabled train and its stops | Offsets are **minutes after midnight of the service date**. First stop: no arrival. Last stop: no departure. A **Pass** has arrival = departure. |
| `ServiceCalendar`, `CalendarException` | Which weekdays a service runs, plus exceptions (Poya) | `DaysOfWeek` is stored as a PostgreSQL integer array. |
| `TrainSet`, `Coach` | A train formation and its coaches | Coach `Position` counts from 1; which end is 1 is not defined yet. |

**Why enums as text:** `Express`, `Slow`, `Up` ... are stored by name, not number, so the data is readable and reordering an enum cannot silently change meaning.
**Why `Guid` ids:** the contract says IDs are UUIDs; they are created in code with `Guid.CreateVersion7()`.

### 3.3 `Persistence/NetworkDbContext.cs` — how EF Core maps the model
**What:** the one class EF Core uses to read and write the Network tables.
**How:** `HasDefaultSchema("network")` puts every table in the `network` schema. `OnModelCreating` sets maximum lengths, stores enums as strings, and defines the **unique indexes**:

| Unique on | Guarantees |
|---|---|
| Station `Code`, Line `Code`, Route `Code`, Service `Number`, Calendar `Code`, TrainSet `Code` | The seed can find a row again by its natural key; no duplicates |
| (Station, platform number), (Route, sequence), (Route, station), (Service, sequence), (Calendar, date), (TrainSet, coach position) | Child rows cannot repeat |

Children (platforms, route stations, stops, exceptions, coaches) are deleted with their parent (`Cascade`); things other rows point at (stations, routes, calendars) are protected (`Restrict`).
**Why:** the database is the last line of defence; even a bug in the loader cannot create a duplicate station.

### 3.4 `Persistence/Migrations/` — the database as code
Generated by `dotnet ef migrations add InitialNetwork` (tool pinned in `.config/dotnet-tools.json`). `…_InitialNetwork.cs` creates the schema, tables and indexes; `NetworkDbContextModelSnapshot.cs` is EF's memory of the current model so the *next* migration can compute the difference.
**Rule:** never edit a migration that has been applied elsewhere; add a new one.
**Command** (from `backend/`, with any connection string set because the tool does not connect):
`dotnet tool run dotnet-ef migrations add <Name> --project src/SmartRail.Network --startup-project src/SmartRail.Api --context NetworkDbContext --output-dir Persistence/Migrations`

### 3.5 `Seed/` — loading the data safely
The seed files live in `shared/seed/` (see its README for what is real and what is fictional). Loading is split into small steps so the rules can be unit-tested without a database.

| File | Role |
|---|---|
| `SeedData.cs` | The shapes of the files, as records. Text such as `"Express"` stays a string here so the validator can report a bad value clearly. |
| `CsvLine.cs` | Splits one CSV line. Handles quoted fields with commas and `""` for a quote. 20 lines instead of a CSV package. |
| `SeedReader.cs` | Reads `stations.csv` (columns found by header name) and the four JSON files into `NetworkSeedData`. Only reads; decides nothing. |
| `SeedFolder.cs` | Finds the folder: `Seed:Path` if set, otherwise walks up from the running program to `shared/seed`. |
| `SeedDays.cs` | Maps "Mon".."Sun" to `DayOfWeek`. |
| `SeedValidator.cs` | **Pure code, no database.** Checks every rule and collects *all* problems with a `SeedRule` name each. Returns errors (stop) and warnings (log). |
| `SeedDataException.cs` | Thrown with the full list of errors; stops the application. |
| `NetworkSeeder.cs` | Validates, then writes to the database in **one transaction**. |

**What `SeedValidator` checks:** duplicate keys; unknown text values; unknown line / route / calendar / station; sequences without gaps; route distances strictly increasing; stops on the route and in route order; first stop without arrival, last without departure, neither a Pass; both times on every middle stop; a Pass has equal times; departure not before arrival; time never goes backwards; the planned platform exists (1..N); a `distance_source` containing **WRONG** is refused; **ESTIMATE** gives one warning naming the station.

**How `NetworkSeeder` behaves on every start (no duplicates):**
1. Read and validate. If invalid: throw, write nothing.
2. Start a transaction.
3. Delete the child rows (platforms, route stations, stops, calendar exceptions, coaches) — nothing else refers to them.
4. For each station / line / route / calendar / service / train set: find the existing row by its natural key and **update it in place** (its ID never changes), or create it.
5. Add the child rows again, save, commit.

**Watch out:** a station or service removed from the seed files stays in the database, because other modules may already refer to it.
**Conventions agreed:** platforms are "1".."N" from the CSV count; Pass stops carry the passing time in both fields; the Kandy distance is an ESTIMATE until the SLR Matale figure replaces it.

### 3.6 `Features/SearchStations/` — one feature = one folder
`GET /api/v1/stations?query=` (contract §3.1, public, type-ahead, at most 10).

| File | Role |
|---|---|
| `SearchStationsEndpoint.cs` | Maps the URL (`[Required]` on `query` only makes the description say it is required; the real check is our validator, so the error keeps the contract's shape), calls the validator, turns a problem into a `400` Problem Details with `code: VALIDATION_FAILED`, `errors.query` and `requestId` (contract §6). `AllowAnonymous` = policy *Public*. |
| `SearchStationsValidator.cs` | The query must not be blank or longer than 50 characters. |
| `SearchStationsHandler.cs` | The query: name or code contains the text (case-insensitive `ILIKE`); names that **start with** the text first, then alphabetical; `Take(10)`. `%`, `_` and `\` in the user's text are escaped so they match literally. EF Core parameterises the query (no string-built SQL). |
| `StationSummary.cs` | The response row: `{ id, code, name }`. |

**Good to know:** `kan` returns Kandy then Rambukkana, because "Rambukkana" contains "kan".
**Why this layout:** a feature slice keeps endpoint, validation and handler together (CLAUDE.md §3); there is no repository layer and no mediator library.

---

### 3.7 `INetworkSchedule.cs` and `NetworkScheduleReader.cs` — the one door into Network
**What:** other modules never see Network's tables. `INetworkSchedule.GetScheduleAsync()` hands out one `NetworkSchedule` record: the services with their stops, the calendars with their exceptions, and the train sets (id and code). They are plain records copied from the tables.
**How:** `NetworkScheduleReader` (internal) reads the tables and fills the records; it is registered as `INetworkSchedule`.
**Why internal:** `NetworkDbContext`, every class in `Model/` and `NetworkSeeder` are `internal`. A module that tries to read `network.Stations` simply does not compile (architecture Q3/Q5). `InternalsVisibleTo` gives the **integration tests only** a view of the internals, so they can check what the seed loader wrote.
**Watch out:** Network has its own call type in the model and a public `ScheduledCallType` in the interface; LiveOperations.Domain has a third. They are copied across the boundary on purpose, so no module depends on another's types.

## 4. The LiveOperations module (`SmartRail.LiveOperations` and `.Domain`)

Only **daily journey generation (US-B2)** exists so far: for today and the next 2 days, every service that runs on a date gets one *operational journey*, stored with all its stops. Delay, hold, tracking and the other statuses will come with their own rules.

```
 every hour (and at start) ► GenerateJourneysHandler
        reads the plan ◄── INetworkSchedule (Network)        reads trains ◄── service-assignments.json
                       └────────────► JourneyPlanner (pure: which journeys are missing?) ◄───────┘
                                              └► OperationalJourney.Plan (Domain: offsets → UTC) ► ops.Journeys, ops.JourneyStops
```

### 4.1 `LiveOperations.Domain/OperationalJourney.cs`
**What:** the aggregate: `Id`, `ServiceId`, `ServiceDate`, `TrainSetId`, `Status` (only `Planned`) and its `Stops` (sequence, station, Stop/Pass, scheduled arrival and departure in **UTC**, planned platform). Driver and guard are not here yet (see open points).
**How:** `OperationalJourney.Plan(...)` is the only way to create one. It takes the timetable's offsets (minutes after *local* midnight of the service date) and a `TimeZoneInfo`, and converts each to UTC once: local midnight + offset, then `TimeZoneInfo.ConvertTimeToUtc`. An offset above 1440 (an overnight run) lands on the next calendar day but stays in this journey (E-12).
**Why in Domain:** it is a rule of how a journey gets its times. The project references nothing but `System`, reads no clock and is checked by the architecture test. The private constructors exist only so EF Core can fill the objects.

### 4.2 `Persistence/LiveOperationsDbContext.cs` and the `InitialOps` migration
**What:** the module's tables in schema **`ops`**, with its own migration history table inside `ops`. `Journeys` has a **unique index on (ServiceId, ServiceDate)**; `JourneyStops` is an owned collection (key: journey + sequence) because stops are never read without their journey.
**Why:** the database itself guarantees no service runs twice on a date (A-13), whatever the code does. Times are `timestamptz`; status and call type are stored as text.

### 4.3 `Features/GenerateJourneys/`
| File | Role |
|---|---|
| `JourneyPlanner.cs` | **Pure.** Given the schedule, the train set per service and the journeys already stored, returns the missing ones for today and the next 2 days. "Today" is the date in **Asia/Colombo**, not in UTC. A calendar exception wins over the weekly pattern (`Runs=false` skips a Poya day; `Runs=true` adds a run). Uses the injected `TimeProvider`. |
| `GenerateJourneysHandler.cs` | The database part: read the schedule, check the assignments, read the stored (service, date) pairs, ask the planner, **insert** what is missing. It never updates or deletes, so a timetable edit cannot change a journey that exists (A-17). |
| `ServiceAssignments.cs` | Reads `shared/seed/service-assignments.json` (service number → train set code) and checks it: every service has a train set, no assignment names an unknown service or train set. |
| `SeedFolder.cs` | Finds the seed folder (`Seed:Path` or `shared/seed`). A copy of the Network one, because modules do not share code. |
| `JourneyGenerationService.cs` | A `BackgroundService`: runs once at start, then every hour (`PeriodicTimer` on the injected clock). A failed run is logged and retried at the next tick; it never stops the application. |

### 4.4 `LiveOperationsModule.cs`
`AddLiveOperationsModule` registers the DbContext (`ops` history table), the planner (with the Colombo time zone), the handler and two hosted services, **in this order**: `LiveOperationsStartupService` (Development: apply migrations; elsewhere: refuse to start if migrations are pending; then check the assignments, so a wrong file **stops the application**) and then `JourneyGenerationService`.

### 4.5 Design decisions (one line each)
| Decision | Reason |
|---|---|
| Insert only, never update | A-17: generated journeys are not changed by timetable edits |
| Hourly run | It only inserts, so it is safe to repeat, and it cannot miss a day |
| Read all stored (service, date) keys each run | About a dozen rows a day; simplest. Revisit when journeys are archived |
| Assignments read from the file on every run | No second copy to keep in step; the start-up check already proved the file |
| Assignment mistakes stop the start; run failures only log | A wrong file is a configuration error to fix; a brief database outage is not |
| Planner and `ServiceAssignments` are `public` | They are pure, and the unit tests need them. Everything touching tables is `internal` |
| `Guid.CreateVersion7(now)` with the injected clock | Time-ordered ids without reading the system clock |

## 5. Tests

| Project | What it proves | Needs |
|---|---|---|
| `tests/Unit` | Seed rules (`Network/Seed/SeedValidator*Tests`), CSV splitting, the **real** `shared/seed` files; the journey rules: the four B-2 tests, UTC conversion and overnight stops (`LiveOperations/OperationalJourneyTests`), `JourneyPlannerTests` (Colombo date, exceptions, train set), `ServiceAssignmentsTests` | Nothing |
| `tests/Integration` | The real application on a real PostgreSQL: seed counts, `network` schema, restart does not duplicate, WRONG data stops startup, the ESTIMATE warning, the endpoint, the permission matrix; **journey generation** (12 journeys and 132 stops for the 3 days, UTC storage, a restart adds none, `ops` schema, the database refuses a duplicate, a wrong assignment file stops the start) | **Docker running** |
| `tests/Architecture` | Domain projects reference only `System.*`; no module depends on another except Disruptions → LiveOperations | Nothing |
| `tests/Integration` → `OpenApiDocumentTests` | The running application's `/openapi/v1.yaml` equals the committed `backend/openapi.yaml`. An API change that is not reflected in the file fails the build (and CI). | Docker |
| `tests/Contract` | (empty so far) | |

`ApiFixture` starts one PostgreSQL container and one copy of the application for all integration tests. The application's clock is a `FakeTimeProvider` fixed at **Wednesday 14 Oct 2026, 10:00 in Colombo**, so "today and the next 2 days" are three weekdays and the expected counts never depend on the real date. The journey tests read the tables with plain SQL, so they do not depend on the module's internals. Run everything: `dotnet test backend/SmartRail.sln`.
**After changing an endpoint:** run the tests once with `UPDATE_OPENAPI=1` (PowerShell: `$env:UPDATE_OPENAPI="1"; dotnet test backend/SmartRail.sln; Remove-Item Env:UPDATE_OPENAPI`) to rewrite `backend/openapi.yaml`, then read `git diff backend/openapi.yaml` and compare it with the contract (CLAUDE.md §5.3).
`PermissionMatrixTests` holds one row per endpoint and role; only the anonymous role exists until sign-in is built.

---

## 6. Running it locally

1. Copy `.env.example` to `.env`, set `POSTGRES_PASSWORD`, then `docker compose up -d`.
2. `dotnet user-secrets set "ConnectionStrings:SmartRail" "Host=localhost;Database=smartrail;Username=smartrail;Password=<your password>" --project backend/src/SmartRail.Api`
3. `dotnet run --project backend/src/SmartRail.Api` — in Development it migrates and seeds, then serves `/api/v1/stations?query=kan`.

---

## 7. Open points

| # | Point | Status |
|---|---|---|
| 1 | `backend/openapi.yaml` | **Done (13 Oct):** YAML served by the app and compared in a test (the build-time generator can only write JSON, so that package was removed) |
| 2 | `MapOpenApi()` is public in all environments | Decide before release |
| 3 | Which end is coach position 1 | Undefined; no story needs it |
| 4 | Kandy distance is an ESTIMATE | Replace with the SLR Matale figure |
| 5 | Permission matrix has only the anonymous role | Add roles when sign-in exists |
| 6 | The error schema in `openapi.yaml` must include `code`, `errors`, `requestId` (contract §6) | **Done (13 Oct)**; a test checks it |
| 7 | `openapi.yaml`: no `servers` entry, `query` required | **Done (13 Oct)**; tests check both |
| 8 | The `ProblemDetails` description says `code` and `requestId` are always present; that is true for our own errors, not for framework errors (for example a malformed request) | Watch when more endpoints exist |
| 9 | Journeys have **no driver and no guard yet**. `service-assignments.json` names them, but staff accounts do not exist until Identity seeds them (decision 8, 14 Oct). Add them when Identity exists; until then nothing is invented | Open |
| 10 | Generation reads **all** stored journey keys every hour | Fine for the MVP; revisit when journeys are archived |
| 11 | `TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo")` needs the time-zone database: present on Windows and on `ubuntu-latest`; a minimal container image would need `tzdata` | Check when a Docker image is built |
| 12 | Network's seed loader still creates ids with `Guid.CreateVersion7()`, which reads the system clock inside the call | Harmless (ids only); switch to the injected clock if a rule ever needs it |
| 13 | The domain model §10 does not list the four B-2 test names; the names were taken from the task | Add them to §10 if you want the document complete |

---

## 8. Change log

| Date | Change |
|---|---|
| 13 Oct 2026 | First version: scaffold, Network module (model, migration, seed loader, `GET /stations`), tests |
| 13 Oct 2026 | OpenAPI: `ProblemDetails` gets `code`, `errors`, `requestId`; `query` required; no `servers` entry (three tests) |
| 14 Oct 2026 | US-B2: Network made internal with the public `INetworkSchedule`; LiveOperations gets `OperationalJourney` (Domain), the `ops` schema and migration, the journey planner, handler and hourly background service, the service-assignments check; architecture test allows LiveOperations → Network; FakeTimeProvider in the tests |
| 13 Oct 2026 | `openapi.yaml`: served at `/openapi/v1.yaml` and checked by `OpenApiDocumentTests`; removed `Microsoft.Extensions.ApiDescription.Server` and the build-time workaround |
