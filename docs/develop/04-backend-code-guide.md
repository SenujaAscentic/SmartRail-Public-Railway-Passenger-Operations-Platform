# SmartRail – Backend Code Guide

**Written for:** Senuja, to read before a review (§28) and to explain the code without the AI.
**Kept up to date by:** the coding agent, after every change to backend code (CLAUDE.md §5, step 9).
**Last updated:** 18 Oct 2026 · **Covers:** **public read endpoints for the apps and the stream event schemas (section 4.14),** **LiveOperations live state, the transactional outbox, the dispatcher and the public SSE stream (sections 4.7 to 4.13),** solution scaffold, Network module (model, migration, seed, `GET /stations`, public schedule interface, `IStationDirectory`), LiveOperations (daily journey generation, US-B2), **Identity (staff sign-in, crew phone and cab sign-in with cab sessions, refresh with a 10 s grace window, sign-out, policies, staff and cab-device seed; US-O1, US-B7, US-C1, US-C2)**, **the pure rules R-01 to R-06 in `LiveOperations.Domain`**, Api host, API description (`openapi.yaml`), tests.

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
| `SmartRail.Api` | The host: starts the program, connects the modules, serves HTTP | Network, LiveOperations and Identity wired in; authentication and authorisation in the pipeline |
| `SmartRail.Network` | The timetable data: stations, routes, services, train sets | **Built** (this guide) |
| `SmartRail.LiveOperations` / `.Domain` | Today's journeys and the delay/ETA rules | **Built:** daily journey generation (4.1 to 4.5); the live rules wired into the journey (4.6, 4.7); the transactional outbox, the dispatcher and the public SSE stream `GET /stream` (4.8 to 4.13) |
| `SmartRail.Disruptions` / `.Domain` | Incidents | Empty |
| `SmartRail.Identity` | Who is calling and may they do this: staff sign-in, tokens, policies | **Staff, crew phone and cab sign-in built** (section 5); passengers, display devices, provider not yet |
| `SmartRail.Notifications`, `.CateringIntegration` | Push, catering | Empty |
| `SmartRail.Simulator` | Replays GPS through the API | Empty |

---

## 2. The Api host

### `SmartRail.Api/Program.cs`
**What:** the first code that runs. It builds the web application and connects the modules.
**How:**
1. `AddNetworkModule(...)` registers the Network module's database and startup work.
2. `AddOpenApi()` / `MapOpenApi("/openapi/{documentName}.yaml")` make the application describe its own API at `/openapi/v1.yaml` (OpenAPI, as YAML). The committed copy is `backend/openapi.yaml`; see section 6. Two *transformers* adjust the description here, once for every module: one removes the `servers` entry (it would be copied from whichever request produced the file), and one adds `code`, `errors` and `requestId` to the `ProblemDetails` schema, because the framework only knows the standard fields and contract §6 says every error carries them. The error responses themselves are still built where the error happens.
3. `app.MapGroup("/api/v1")` creates the URL prefix every endpoint lives under (contract §1); `MapNetworkEndpoints()` puts the Network endpoints in it.
4. `AddIdentityModule(...)` (after Network) registers staff sign-in and the authentication / authorisation services; `UseAuthentication()` then `UseAuthorization()` put them in the request pipeline (who is calling, then may they), and `MapIdentityEndpoints()` maps the three auth endpoints. Two more transformers describe the **Bearer** scheme in `openapi.yaml` and mark every operation whose endpoint names a policy as needing it. The API description itself is `AllowAnonymous()`.
5. `AddSingleton(TimeProvider.System)` registers the one clock of the whole application; tests replace it with a fixed one. `AddNetworkModule` is called **before** `AddLiveOperationsModule` because hosted services start in the order they are registered and LiveOperations' start-up check needs the network data that Network loads first.
6. **A `Date` header on every response** (a small middleware, `OnStarting`): it is the time of the injected clock, the same clock as every stored time, so the passenger app can show "updated X ago" as `Date` minus `freshness.updatedAt` (contract §1). Real Kestrel keeps an application-set `Date` and does not add a second one (checked with a throwaway app, 18 Oct); the in-memory test host adds none at all, which is why the test can only pass because the application sets it. A browser app on another origin would also need `Access-Control-Expose-Headers: Date`, because `Date` is not a CORS-safelisted response header (there is no CORS setup yet).
7. `public partial class Program;` at the bottom exists only so the integration tests can start the whole application in memory.

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

### 3.8 `IStationDirectory.cs` and `StationDirectory.cs` — the second door into Network
**What:** a public interface with one method, `GetStationIdsAsync(codes)`, that answers a station code ("FOT") with the station's ID. Codes that do not exist are left out of the answer so the caller can report them.
**Why:** Identity needs it once, while seeding station managers (their token carries a station UUID). It is kept apart from `INetworkSchedule` (the whole plan) so the door stays small. The IDs stay the same across restarts because the network seed finds each station by its code and updates it. Tested in `tests/Integration/Network/StationDirectoryTests`.

## 4. The LiveOperations module (`SmartRail.LiveOperations` and `.Domain`)

Three things exist so far. **Daily journey generation (US-B2)**: for today and the next 2 days, every service that runs on a date gets one *operational journey*, stored with all its stops (4.1 to 4.5). **The live rules wired into the journey**: positions and crew taps become stop times, delay, tracking and domain events (4.6, 4.7). **The transactional outbox, the dispatcher and the public event stream** that carry those events to every client (4.8 to 4.13). Hold, cancel, terminate and the other decisions come with their own stories.

```
 every hour (and at start) ► GenerateJourneysHandler
        reads the plan ◄── INetworkSchedule (Network)        reads trains ◄── service-assignments.json
                       └────────────► JourneyPlanner (pure: which journeys are missing?) ◄───────┘
                                              └► OperationalJourney.Plan (Domain: offsets → UTC) ► ops.Journeys, ops.JourneyStops
```

### 4.1 `LiveOperations.Domain/OperationalJourney.cs`
**What:** the aggregate: `Id`, `ServiceId`, `ServiceDate`, `TrainSetId`, `LineId` (the line of its service, for the stream envelope), `Status` (only `Planned`), the live state of 4.7, and its `Stops` (sequence, station, Stop/Pass, scheduled arrival and departure in **UTC**, planned platform). The driver is not here yet (see open points); a journey will have a driver only, the guard role is deferred.
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

### 4.6 `LiveOperations.Domain` — rules R-01 to R-06 as pure value objects and functions
Built on `feature/live-rules` by a second agent ("agent B"), merged into `develop` on 15 Oct (commit `e4f6d1e`), **test-first with the approved names of domain model §10**. **Wired into `OperationalJourney` in 4.7.** Every file reads no clock (times are passed in) and uses only `System` types, so the architecture test keeps the project pure. This section is written from the code and the commit message; agent B's own summary was not available to the agent that wrote this guide.

| File | Rule | What it does and why it is written that way |
|---|---|---|
| `Delay.cs` | R-01 | `Delay(int Minutes)`, signed whole minutes (`IsLate`, `IsEarly`, `IsOnTime`). `Delay.Between(scheduled, actual)` divides the tick difference by ticks-per-minute, which **drops the fraction towards zero**: 59 s late = 0, 119 s late = 1, 59 s early = 0. Integer division is the whole rule, so there is no rounding call to get wrong. |
| `ExpectedTimes.cs` | R-02, R-03 | `Arrival` = scheduled + delay (a negative delay carries forward). `Departure` = scheduled + `max(0, delay)`: a predicted departure is **never earlier than scheduled**. An actual early departure is not predicted; it is recorded as it happened and `Delay.Between` shows it negative. |
| `SourcePriority.cs` | R-05 | `Source` enum with the priority as its number (Controller 1, Crew 2, Gps 3, Schedule 4; **lower wins**). `TimeWithSource.Resolve(current, incoming)` keeps the current value only when it comes from a strictly higher-priority source, so the **same source may overwrite itself** (newer GPS replaces older GPS). `SourcedDelay.Apply` is **not** the same rule: since the R-05 clarification of 15 Oct the journey delay follows the most recent stop event whatever its source, and only a controller delay blocks it (4.7); `ClearController(recalculated)` replaces a controller delay by the delay recalculated from the latest stop time. The server sets `Source` from the credential, never from a request body (CLAUDE.md §4). |
| `StationZone.cs` | R-04 | A pure state machine: `Observe(state, sample)` returns the new state and an event (`Arrived`, `Departed`, `Passed`, `None`). Radius 300 m; "stopped" = speed under 5 km/h for an **unbroken** 20 s (the spell restarts when the train speeds up). Entering and leaving without that spell gives `Passed`; leaving after `Arrived` gives `Departed`. The caller measures the distance to the station, so the rule contains no map geometry (R-07 / R-08 bring that). Samples must arrive oldest first; discarding older ones is R-08. |
| `TrackingFreshness.cs` | R-06 | `Of(lastPositionAt, now)`: under 2 min = `Live`, 2 to under 10 min = `Stale`, 10 min or more = `Lost`. Any accepted position moves `lastPositionAt` to now, which gives `Live` again, so no separate "recovered" code exists. `NotStarted` and `Ended` come from journey start and end, not from time. |

**Tests** (`tests/Unit/LiveOperations`): `DelayTests` (8: R-01 to R-03 together), `SourcePriorityTests` (7), `StationZoneTests` (9), `TrackingFreshnessTests` (4).
**Watch out:** these functions do not say *what a controller override does when GPS later contradicts it* (US-O3.3); that warning belongs to the journey, not to these functions. The warning itself (US-O3.3) is not built yet.

### 4.7 The live state inside `OperationalJourney` (R-01 to R-09, R-23, inference)
Built on `feature/live-journey` (15 Oct) and merged. The aggregate now *uses* the rules of 4.6 and collects domain events. **It reads no clock, no database and no HTTP**: every time is passed in, and the slice that saves the journey does the rest.

| Member | What it does |
|---|---|
| `RecordPosition(PositionSample, now)` | One position from the cab tablet. Event time from R-09 (`ClockCorrection.EventTime`: GPS time, else device time + the clock offset). Then R-08 (`PositionPlausibility.Check`: not newer than the last accepted, more than 500 m from the route, more than 120 km/h since the last one) → a `PositionVerdict`; a discarded position **changes nothing and raises nothing**. An accepted one makes tracking `Live`, then runs R-04 (`StationZone.Observe`) for every stop the slice gave a distance for. |
| `RecordCrewTime(stationId, kind, occurredAt)` | A crew tap (source = Crew). An unknown station is an `ArgumentException`; the slice has already checked it is on the journey. |
| `SetControllerDelay` / `ClearControllerDelay` | The controller override (R-05). Clearing **recalculates** from the most recent recorded stop time (R-01), and only goes to "on schedule" when no stop event exists: a reset to 0 would show a false "On time" (US-P4 ★). Neither bumps `controlVersion`, which does not exist yet (the decisions slice adds it). |
| `RefreshTracking(now)` | R-06 (`Live` → `Stale` → `Lost` by the age of the last position) **and R-23** (the overdue delay). Must be called at least every 30 s by a background service (Phase 2), so lateness grows "within 1 minute" (US-P4 AC1 ★). |
| `DomainEvents`, `ClearDomainEvents()` | The facts this journey announced since it was loaded: `StopStatusChanged`, `DelayChanged`, `DataFreshnessChanged` (domain model §8.2 names). The outbox interceptor (4.8) reads and then clears them. |
| `JourneyStop.ExpectedArrival(delay)` / `ExpectedDeparture(delay)` | Computed on read, never stored: what happened wins, otherwise R-02 / R-03. |

**State is stored as plain columns** (delay minutes + source, tracking, last position, and per stop: status, actual times and their sources, zone phase), not as value-object types, so EF Core maps it with **no configuration inside the Domain project**; the `Source`, `StopStatus`, `ZonePhase` and `TrackingStatus` enums are stored as **text** like the other enums. Migrations `LiveJourneyState` (the columns) and `JourneyLineId`. The values and their sources are exposed again as `CurrentDelay`, `ActualArrivalTime`, … (read-only, so EF ignores them).

**The rules as they are wired (each was a decision by Senuja, 15 Oct):**
| Rule | What the code does |
|---|---|
| R-01 | `Delay.Between` **truncates toward zero** (59 s early is "on time"). The domain model §5 says so. |
| R-05 (clarified) | Source priority protects **the same stop's time** (`TimeWithSource.Resolve`): GPS cannot overwrite a crew tap at that stop. **The journey delay follows the most recent stop event whatever its source** (`SourcedDelay.Apply`); only a controller delay blocks it, until cleared. A late-delivered older event stores its stop time but does not pull the delay back (`FollowDelay`). Without this, one crew tap would freeze the delay for the rest of the trip. |
| R-04 | `Arrived` is timed **when the train stopped** (the first slow sample in the zone), not when the 20 s were complete. A `Passed` stop records its passing time (source GPS) as both arrival and departure and **updates the delay** from it (express trains otherwise go up to 46 km without a delay update). |
| R-04 inference | When a stop gets an actual time, every **earlier stop still Scheduled** is inferred **without a time and without touching the delay**: `Departed` if its call type is Stop (the origin too), `Passed` only if its call type is Pass. "Passed" at a scheduled stop would tell passengers the train did not stop there. `InferEarlierStops` also marks its zone finished, so later GPS noise cannot detect it again. Without it a missed GPS detection would leave R-23 measuring against a station the train has gone through, and the delay would grow forever. |
| R-23 | `ExpectedTimes.OverdueDelay`: while a tracked journey has not arrived at or passed its next stop, the shown delay is the larger of the last known delay and (now − scheduled time at that stop), whole minutes toward zero. Stored with source `Schedule` (it is worked out from the timetable and the clock) and replaced by the next real stop time. A controller delay wins. A journey that was never started (`Tracking = NotStarted`) is **not** tracked and gets no overdue delay. |

**Tests** (`tests/Unit/LiveOperations`): `OperationalJourneyLiveTests` (GPS and crew into stop times, status, delay, events; plausibility; clock correction; freshness), `StopSequenceTests` (inference and "the delay follows the latest event"), `OverdueDelayTests` (R-23, pure and in the journey), `PositionRulesTests` (R-08, R-09, haversine), plus the R-01 to R-06 tests of 4.6. A late tap for an inferred stop is covered (it keeps its time, not its status; R-20 will settle out-of-order taps).
**Watch out:**
- **R-07 is not checked here** (registered device, active cab session, assigned driver, Running journey): the position slice must, and the permission matrix and integration tests must prove it (threat T-19 now relies on it, plus R-08).
- **Apps must show "Live information unavailable", never "On time", for a never-started journey** (to go into `shared/status-fixtures.json`).
- **Anti-flicker (US-P4 AC2: a displayed delay changes only after two consecutive updates) is not in the aggregate**; it belongs to the position slice (Phase 2, to be agreed).
- The journey has **no driver yet** (open point 9).

### 4.8 The transactional outbox (`Outbox/`)
**Why:** announcing a fact (architecture principles) must succeed or fail *together* with the state change, and a stream client must never hear about something that was not saved.

```
 slice: journey.RecordCrewTime(...)  ──►  journey.DomainEvents = [StopStatusChanged, DelayChanged, ...]
 slice: db.SaveChanges()  ─►  OutboxInterceptor (SavingChanges): one OutboxEvents row per event, added to the SAME SaveChanges
                          ─►  PostgreSQL transaction: journey rows + outbox rows commit together, or neither
                          ─►  OutboxInterceptor (SavedChanges): journey.ClearDomainEvents()   (only after success)
```

| File | Role |
|---|---|
| `OutboxEvent.cs` | One row of `ops."OutboxEvents"`: identity `Id` (insertion order), `EventId`, `Type`, `OccurredAt` (the corrected event time), `CreatedAt` (TimeProvider), `JourneyId`, `Json` (jsonb: the full envelope **including source**), `Status` (`Pending`/`Dispatched`/`Failed`, text), `Attempts`, `Sequence` (null until dispatched; unique), `DispatchedAt`. |
| `EventEnvelope.cs` | The envelope of domain model §8.1 (`eventId`, `type`, `version`, `occurredAt`, `journeyId`, `serviceDate`, `stationIds`, `lineId`, `source`, `payload`) and the three payload records of §8.2. `EventJson.Options` is the one set of JSON rules (camelCase, enums as names, null fields left out) used by the outbox, the stream and the examples. **`sequence` is not in the stored envelope**: it is given at dispatch. **`causedBy` is left out** until the decision endpoints exist (nothing here knows an actor or a request id). |
| `EventMapper.cs` | Domain event → envelope. `stationIds`: `StopStatusChanged` its own station; `DelayChanged` and `DataFreshnessChanged` every remaining stop's station (Scheduled or Arrived), so every display downstream hears the new delay. `DelayChanged.stops[]` and `lastStationId` are worked out from the journey **as it is at save time**. `DataFreshnessChanged` has source `Gps` (it is about the age of GPS data). |
| `OutboxInterceptor.cs` | An EF Core `SaveChangesInterceptor`, registered on `LiveOperationsDbContext`. Adds rows in `SavingChanges`, clears the aggregate's list in `SavedChanges`. |
| Migration `OutboxEvents` | The table, its indexes and the `ops."EventSequence"` database sequence. |

**Tests:** `EventMapperTests` (unit); `OutboxTests` (integration: rows written in the `ops` schema with TimeProvider times; the list is emptied only after success; **a failed save writes no rows and keeps the events**).
**Watch out:** only journeys **tracked by the DbContext** are looked at (an `AsNoTracking` journey that is changed and saved announces nothing).

### 4.9 The dispatcher (`Outbox/OutboxPump.cs`, `OutboxDispatcher.cs`)
`OutboxDispatcher` is a `BackgroundService` registered **after** the start-up and generation services (so the schema exists). Every 250 ms (`Outbox:PollMilliseconds`) it calls `OutboxPump.DispatchBatchAsync`; a full batch (100) repeats at once. Every minute it calls `DeleteOldAsync`.

| Behaviour | How, and why |
|---|---|
| Order and the **sequence** | Oldest `Pending` first (by identity `Id`). The sequence is taken from `ops."EventSequence"` **here, one row at a time**, not when the row is written: two transactions can commit in the opposite order to their start, and a client must never see sequence 10 before 9. A database sequence never goes back, even after old rows are deleted. |
| Hand-over | `IEventPublisher.Publish(PublicEvent)` (the stream hub). The row is projected for the public stream first (`PublicProjection`); a type that is not public (none yet) is still marked dispatched, for a later staff stream. |
| Marking | One `SaveChanges` per row. A crash between publishing and marking sends that row again after restart under a **new sequence**: **at-least-once**, which is why every event has an `eventId` that consumers ignore the second time. |
| Failure | `Attempts` + 1, the sequence is taken back, the row stays `Pending`; at **5** attempts it becomes `Failed` (logged once as an error). Later rows are not blocked. |
| Retention | `Dispatched` and `Failed` rows older than **24 h** are deleted (contract §4: resume works for 24 h). `Pending` rows are never deleted. |
| Logs | **Ids, types and counts only; for an exception only its type name**, never its message or the payload (CLAUDE.md §6). A test checks it. |
| **Real timers** | The wait between polls is a real `Task.Delay`, not `TimeProvider`: it is scheduling, not a business timestamp, and the tests' frozen clock would never wake it. **A deliberate exception to the TimeProvider rule** (agreed 16 Oct); every stored timestamp still comes from `TimeProvider`. The stream's heartbeat wait is the same exception. |

**Tests:** `OutboxDispatcherTests`: dispatched within a second (limit 2 s), increasing sequences in insertion order, higher sequences for later events across journeys, **retry then give-up after 5 attempts while a healthy journey still flows** (a `FaultInjectingPublisher` in the *test* project stands in front of the hub), logs hold ids only, 24 h deletion.
**Watch out:** **one dispatcher instance** is assumed (the monolith runs once). The unique index on `Sequence` would stop two instances from corrupting the stream, but they would also compete for rows; scaling out needs a lock (`FOR UPDATE SKIP LOCKED`) and a shared fan-out (architecture Q8).

### 4.10 The public stream: `GET /api/v1/stream` (`Stream/` and `Features/StreamEvents/`)
Contract §4, **policy Public**: the endpoint says `AllowAnonymous()` itself, because default-deny is on for the whole application (`PermissionMatrixTests` has a row for every role).

```
 client ──GET /stream?journeyIds=..&stationIds=..&lineIds=..  [Last-Event-ID: n]──►  StreamEndpoint (validate → 400 or run)
   StreamHandler: 1 subscribe to the hub   2 read newest dispatched sequence   3 snapshot / replay / resync   4 live events + heartbeat
   dispatcher ──► PublicProjection (allow-list) ──► StreamHub ──► every matching Subscription (its own buffer) ──► the open response
```

| File | Role |
|---|---|
| `Stream/PublicEvent.cs`, `PublicProjection.cs` | **T-07.** The public event is the envelope **without `source`** (and without `causedBy`) plus the sequence. `PublicProjection` is an **allow-list**: only event types and payload fields named there are ever sent (`PublicEventTypes` is what the examples test enumerates), so a field added to an event later stays private until someone decides otherwise. |
| `Stream/StreamHub.cs` | `StreamFilter` (an event matches if it matches **any** of the three lists: a client lists everything it cares about, one connection), `Subscription` (a bounded channel of 1000), `StreamHub` (singleton fan-out). A subscriber whose buffer is full is **cut off** (`SubscriberTooSlowException`) and resumes with `Last-Event-ID`: it must neither block the dispatcher nor grow memory without limit. |
| `StreamRequest.cs` | Parses the three comma-separated GUID lists (max **50** ids each, bounding the snapshot query, T-10) and `Last-Event-ID`. **At least one filter is required** (else `400 VALIDATION_FAILED`, errors per field), so nobody can ask for the whole network. A bad id is not echoed back. |
| `ResumeDecision.cs` | `Fresh` (no id), `Resume` (id within what the log keeps, or the one just before the oldest), `Resync` (anything else). Sequences have gaps, so the log cannot prove nothing was deleted; the safe rule is: a false "resync" costs one snapshot reload, a false "resume" would lose events silently. |
| `SnapshotReader.cs` | One `snapshot` per matching journey: delay, tracking, last seen, last position, and per stop its status, actual and expected times. **Public fields only.** By id (any date), or today's journeys (and yesterday's still running past midnight, E-12) that call at a named station or run on a named line. `sequence` = the stream position the snapshot is current to. |
| `StreamHandler.cs` | The order above. **Subscribing first** is what makes "nothing is lost between the snapshot and the live part"; events at or below the snapshot's sequence are skipped; one that is in both is harmless because events say "the value is now X". Heartbeat: a comment line `: keep-alive` every 15 s (`Stream:HeartbeatSeconds`), via a real timer. Headers: `text/event-stream`, `Cache-Control: no-store`, `X-Accel-Buffering: no`, buffering off. |
| `SseFormat.cs` | The text of a frame. **A line break in the type or data is refused** (it would end the frame and let content pose as another field). The response is **written by hand** (`StreamHandler.WriteAsync`) rather than with the built-in `TypedResults.ServerSentEvents`, which cannot write the comment line the contract requires for the heartbeat. |

**What a client receives:** `snapshot` (id = its sequence) for each journey; then `StopStatusChanged`, `DelayChanged`, `DataFreshnessChanged` with `id: <sequence>`; `resync` (no id) when it cannot be caught up, followed by snapshots; comment lines as heartbeat. After a disconnect the client sends `Last-Event-ID` and gets only what it missed.
**Tests:** unit `StreamRequestTests`, `StreamHubTests`, `PublicProjectionTests`; integration `StreamTests` (400s; headers; the snapshot and what it reflects; station and line filters; live events with increasing ids; **no source, staff or session data on the wire**; another journey's events not delivered; replay without a snapshot; nothing repeated; resync; heartbeat; fields equal the shared examples) and `StreamLatencyTests` (below).
**Gate G2 / SM-3:** `StreamLatencyTests` loads a **real generated journey**, changes it through the aggregate, saves, and times how long a connected client waits for the event: **about 0.25 s** measured, limit 5 s. **No application endpoint is involved** (none is added for a test).

### 4.11 `shared/events/` — one example per stream event (CLAUDE.md §5, step 5)
`StopStatusChanged.json`, `DelayChanged.json`, `DataFreshnessChanged.json` (what is in `data:` of the frame), `snapshot.json`, `resync.json`. `EventExamplesTests` (a) parses every example into the real types, (b) fails if a public event type has no example, (c) checks the examples show no staff fields, and (d) compares the **shape** (every property path and its kind: guid, date-time, date, string, number…) of each example with what the backend really serialises; `StreamTests` also compares the fields of frames on the wire. The check was shown able to fail by deleting a field from an example. **Change the example in the same commit as the event.** The web and Flutter tests parse the same files.

### 4.12 Design decisions of this round (one line each)
| Decision | Reason |
|---|---|
| Outbox, dispatcher, hub and endpoint live **inside LiveOperations** (CLAUDE.md §9 changed) | It is the only event producer; Api would have had to read `ops` tables (forbidden). Api only calls `MapLiveOperationsEndpoints()` |
| Rows written by an EF `SaveChangesInterceptor`, cleared after success | Same transaction for free, no change to any slice, and a failed save loses no event |
| Sequence given at dispatch, from a database sequence | Commit order can differ from start order; the single dispatcher sends in order |
| Stored envelope keeps `source`; the public one is built from an allow-list | The staff stream will need it later; T-07 can never be broken by adding a field |
| One table is both outbox and 24 h replay log | No second store; resume reads the same rows |
| Filters are alternatives (OR) | One connection can follow saved trips and a home station |
| `snapshot` is an SSE event of its own, sent on connect and after `resync` | The public journey endpoints do not exist yet, and a client needs one consistent way to start |
| Slow client cut off, not buffered | Protects the dispatcher and memory; resume repairs it |
| Test-only fault injection lives in the test project | "No test-only endpoints in the application" |
| `LineId` added to `ScheduledService` and the journey | The envelope needs it (`lineId`); Network answers route → line |

### 4.13 How a second event producer joins later (Disruptions)
CLAUDE.md §9 used to put "SSE, outbox dispatcher" in `SmartRail.Api`; it now says they live in LiveOperations. When **Disruptions** needs to announce something (announcements, US-O9), it must **publish through a small public LiveOperations interface** (Disruptions → LiveOperations is an allowed dependency; the reverse is not), so **all events share one table and one sequence and `Last-Event-ID` resume keeps working** for every client. To be designed with US-O9: the interface (roughly "announce this event for these stations"), and how the write stays in the **same transaction** as Disruptions' own state change (two `DbContext`s in one database: pass the transaction, or write both through one connection). If a producer ever has to live outside LiveOperations, the hub and endpoint move to Api behind an `IEventPublisher`-style interface.

### 4.14 Public read endpoints for the apps (Phase 2a, 17 Oct)
Four public endpoints and the event schemas the passenger app and the web apps generate their types from. Contract §3.1 / §5 describe them (the shapes it lacked were added on 17 Oct). **All of them are `AllowAnonymous()`, show no source or staff field (T-07), and never calculate a time: every time comes from the Domain.**

| Piece | What and why |
|---|---|
| `Network/INetworkCatalog.cs`, `NetworkCatalog.cs` | A third public door of Network (after `INetworkSchedule` and `IStationDirectory`): station names, service number / train type, and a train set with its coaches, as plain records (enum values as names). LiveOperations needs them to describe a journey and may not read Network's tables. Smaller than first proposed: Network's own endpoints read their own tables directly |
| `Network/Features/GetLines`, `GetStation` | `GET /lines` (line, its two routes, each route's stations in order with distance and coordinates) and `GET /stations/{id}` (platforms in number order, `cateringStation`). The id is read as text so a malformed id is the same `404 NOT_FOUND` as an unknown one |
| `Station.IsCateringStation` + migration `StationCateringFlag` | The seed marks catering stations only inside `stop_types` ("catering station"); `SeedReader` reads that optional column. Phase 4 can use the flag |
| `LiveOperations/Features/SearchJourneys` | `GET /journeys/search`. `SearchJourneysRequest` parses and checks the query (field errors, `limit` 1 to 50, default 10; `date` defaults to today in Colombo; `after` is UTC, no offset is taken as UTC). The handler asks the database for journeys of the date that **stop** (not pass) at both stations, then in memory keeps those where `from` comes before `to`, (**only when the date searched is today**; any other date is listed as the timetable has it, not judged by "now") the train has **not already left `from`** (stop Departed / Passed), and its **expected** departure is not before `after` (so a late train that has not left stays), sorted by scheduled departure. An unknown station is `404` |
| `Features/GetJourney` | `GET /journeys/{id}`: stops with scheduled / expected / actual times, the train set, freshness and the last position. A journey that points at a service or station Network does not have is a data fault and throws (500), not a guess |
| `Features/JourneyViews` | `JourneyResponses.cs` (the public records, which also feed `openapi.yaml`) and `JourneyViewBuilder` (pure, unit-tested). Status: `NotStarted` while nobody has reported on the train (**never `OnTime` without data**), else `OnTime` / `Late` / `Early` with the signed minutes. `name` is "{terminus} {train type}" ("Kandy Express"): the timetable has no service names |
| Fields present from day one | `hold`, `reason`, `from.previousPlatform` are always in the JSON as `null`, and `version` is `0` (`OperationalJourney.ControlVersion`, migration `JourneyControlVersion`) until the controller decisions exist (Phase 3). `platform` is the planned platform. The schema marks them required and nullable, so the apps' generated types do not change later. **The shape of `reason` (`{code, note}`) and `hold` (`{stationId, minutes}`) was chosen now and may need adjusting in Phase 3** |
| `Api/OpenApi/StreamEventSchemas.cs` | Adds the live events, the snapshot, the resync and a `StreamEvent` union (discriminator `type`) to `components.schemas`, and points the stream operation's `text/event-stream` response at the union. The records are in LiveOperations (`Stream/StreamEventSchemas.cs`, `SnapshotReader.cs`); a unit test checks every example in `shared/events` fits them **exactly**, and an integration test checks that no `$ref` in `openapi.yaml` is undefined |
| `Program.cs` JSON options | **Numbers are strict and enums are sent by name** (`JsonStringEnumConverter`). Without the first, the generated description says "number or string" for every numeric field; without the second, enums are integers. Both exist so the apps' generated types are clean |

**Tests:** `JourneyViewBuilderTests`, `SearchJourneysRequestTests` (unit); `NetworkReadEndpointsTests` (lines, station, catalog); `JourneyReadTests` (search: ordering, pass vs stop, direction, date, limit, `after`, a late train, a train that has left, today's default, errors; detail: fields, a changed journey, **no source**, 404). Each test makes the journeys it needs from real seeded services on a date in 2027 and **removes them again** (`JourneyCleanup`): the journey-generation test counts every journey, so a leftover would make it fail depending on class order. Mutation check: removing the "already left" filter turns `A_train_that_has_already_left_the_departure_station_is_hidden` red. Permission matrix: `/lines`, `/stations/{id}`, `/journeys/search`, `/journeys/{id}` for every role.
**Watch out:** the search compares times in memory (a day has a dozen journeys); revisit if journeys multiply. The platform and hold information is the planned one until Phase 3.

## 5. The Identity module (`SmartRail.Identity`)

Identity answers two questions: **who is calling?** (sign-in, tokens, sessions) and **may they do this?** (policies). Today it covers **staff and crew**: Controller and StationManager on the ops web, Driver on the crew app (phone mode and cab mode). Passengers, display devices and the provider come later. The **guard role is deferred** (14 Oct, domain model D-12), so there are three roles.

```
Staff   POST /auth/staff/sign-in {staffNumber, password}  (Controller, StationManager only)
Phone   POST /auth/crew/sign-in  {staffNumber, password}  (Driver only)
Cab     POST /auth/crew/cab-sign-in  headers X-Device-Id + X-Device-Secret, body {staffNumber, password, deviceTime}  (Driver only)

 each:  validate fields (400) ► [cab only: check the TABLET first: unknown / deactivated / wrong secret = generic 401, and the
        driver's failed-password count is not touched] ► find account ► locked? (429) ► wrong password? count it (401, 429 on the 5th)
        ► right password but the wrong kind of account? the same 401 ► reset the count
        ► [cab only: measure the clock offset (R-09), end the driver's and the tablet's older sessions (R-19), start a CabSession]
        ► ACCESS token (JWT, 15 min)  +  REFRESH token (random secret)
             staff: refresh token in an HttpOnly cookie, 30 min idle, sliding
             crew:  refresh token in the BODY, session ends a fixed 12 h after sign-in (a shift); cab tokens also carry deviceId, crewSessionId, clockOffsetMs

every request:  Authorization: Bearer <access token> ► signature + expiry ► [token has crewSessionId? the cab session must still be active] ► the endpoint's policy

POST /auth/refresh   (staff: cookie only;  crew: body only) ► look up the hash ► revoked? expired? 401
   ► mark used (one atomic statement); already used? allowed ONCE more within 10 s (two tabs), else it is a copy: revoke the whole session (401)
   ► [cab: the session must be active and the tablet still active] ► new access token + new refresh token

POST /auth/sign-out  (same place as refresh) ► revoke the whole session ► [cab: end the CabSession] ► clear the cookie ► 204 (always)
```

### 5.1 `Model/` — the tables as C# classes (all `internal` except `StaffRole`)
| File | What it holds |
|---|---|
| `StaffRole.cs` | `Controller`, `StationManager`, `Driver`. Stored as its name. One role per person, so it is a column and Identity's role tables are not used. |
| `StaffAccount.cs` | A staff member: extends Identity's `IdentityUser<Guid>` (which gives the password hash, lockout counters and security stamp). `UserName` is the **staff number**. Adds `Name`, `Role`, `StationId` (station managers only) and `IsActive`. `MaySignInToOpsWeb` = active Controller or StationManager; `MayUseCrewApp` = active Driver. Both are computed, so EF Core ignores them. |
| `CabDevice.cs` | A registered cab tablet: `Code` ("CAB-01"), `TrainSetCode` (text, the train set belongs to Network), `CredentialHash`, `Active`. **Identity owns this** (domain model D-13); only the hash of the secret is stored. |
| `CabSession.cs` | One driver on one tablet: `AccountId`, `DeviceId`, `StartedAt`, `EndedAt` (empty while active), `ClockOffsetMs`. **Identity owns this** (domain model D-14); it is what makes a cab token valid or not. Cab mode only: a phone sign-in creates none. |
| `RefreshToken.cs` | One refresh token: `TokenHash` (never the secret), `FamilyId` (= one sign-in session), `ExpiresAt`, `UsedAt`, `ReplayedAt` (the one allowed second use), `RevokedAt`; **`AbsoluteExpiresAt`** (crew: the fixed end of the shift; empty for staff) and **`CabSessionId`** (cab: which session this token belongs to); `IsUsableAt(now)`, `CanBeReplayedAt(now)` and the 10 s `GraceWindow`. |

### 5.2 `Persistence/IdentityModuleDbContext.cs` and the migrations
**What:** the module's database access; all tables are in the PostgreSQL schema `identity`. It extends Identity's `IdentityUserContext`, which maps the user table (here `StaffAccounts`) and three tables this module never uses (`AspNetUserClaims`, `…Logins`, `…Tokens`); they stay empty.
**How/why:**
- A **check constraint** makes the database enforce "a station manager has a station, nobody else does" (domain model D-9).
- **R-19 is enforced by the database too**: two *partial unique indexes* on `CabSessions` (`UX_CabSessions_ActivePerDriver`, `UX_CabSessions_ActivePerDevice`, both `WHERE "EndedAt" IS NULL`) allow at most one active session per driver and one per tablet. A new sign-in ends the old ones first; the indexes catch two sign-ins racing each other (the loser gets `409`).
- `CabDevice.Code` and `RefreshToken.TokenHash` have unique indexes (the seed finds devices by code; refresh finds tokens by hash). `Role` is stored as text like every enum.
**Migrations:** `InitialIdentity`, `RefreshTokenReplayedAt` (grace window), `CabSessions` (the table, `AbsoluteExpiresAt`, `CabSessionId`).
**Watch out:** like the other modules, migrations apply automatically in Development only.

### 5.3 `Tokens/`
| File | What it does |
|---|---|
| `JwtSettings.cs` | Reads `Jwt:SigningKey` from configuration (user-secrets / environment, never the repo). Must be at least 32 characters, else the application will not start and the message gives the exact `dotnet user-secrets set` command. Issuer `smartrail` and audience `smartrail-api` are fixed names. |
| `AccessTokenIssuer.cs` | Creates the signed JWT (HS256). Claims: `sub` (account id), `role`, `stationId` (station managers), and for a **cab session** `deviceId`, `crewSessionId`, `clockOffsetMs` (a number), plus `iss`, `aud`, `iat`, `nbf`, `exp`. **No name or other personal data.** Lifetime 15 minutes. Time comes from `TimeProvider`. `CabSessionClaims` is the small record that carries the cab values in. |
| `ClockOffset.cs` | **R-09, pure.** `TryMeasure(serverTime, deviceTime, out offsetMs)`: server minus device, in milliseconds (positive = the tablet runs slow); `false` when the clock is more than 24 hours off. The network delay of the one request is ignored. Event time later = device time + offset. |
| `RefreshTokens.cs` | `Create` makes 256 random bits as the secret and the row to store (only its SHA-256 hash). Staff: `IdleLifetime` 30 minutes, sliding. Crew: pass `absoluteExpiresAt` (`CrewSessionLifetime` = 12 h) and the row never lives past it. `Next(previous)` makes the token that replaces a used one: same family, same fixed end, same cab session. A plain SHA-256 is right here because the secret is random; passwords and the cab-device secret use Identity's `PasswordHasher` instead. |
| `RefreshTokenSource.cs` | Reads the refresh token a request presents and **where it came from**: the body (`{"refreshToken": …}`, crew apps) or the cookie (staff). Each kind is accepted from one place only. |
| `SessionRevocation.cs` | Ends a session: marks every token of the family revoked in one statement. Used by refresh (copy detected, session over) and sign-out. |
| `DummyPasswordCheck.cs` | Does a password check against a throw-away hash, so an **unknown staff number, and an unknown cab device, take as long as a wrong password** and the answer time cannot reveal which exist. The hashes are made once, at start-up. |
| `SessionCookie.cs` | Writes, reads and clears (`Clear`) the cookie `smartrail_refresh`: `HttpOnly`, `Secure`, `SameSite=Strict`, path `/api/v1/auth`. Staff only. |
| `AccessTokenResponse.cs` | The body of sign-in and refresh: `accessToken`, `expiresAt`, and **for crew only** `refreshToken` and `refreshExpiresAt` (left out of the JSON for staff, whose refresh token is never in a body). |

### 5.4 `Authentication/` — what every password sign-in shares
| File | What it does |
|---|---|
| `PasswordSignInRequest.cs`, `PasswordSignInValidator.cs` | The body `{staffNumber, password}` of staff and crew phone sign-in, and its field checks (required, at most 200 characters so nobody makes the server hash a huge password). The cab validator reuses the same field rules. |
| `CredentialCheck.cs` | The part that is the same for staff, phone and cab: find the account (or do the dummy check), honour the lock (`429`), check the password, count failures. It returns the account or the answer to send. **Whether this kind of account may use this endpoint is the caller's decision** (then the same generic 401). |
| `CabSessionCheck.cs` | Runs on **every request that carries a `crewSessionId`** (JwtBearer's `OnTokenValidated`): one indexed lookup of the session. If it has ended, the token is refused at once. Without this, a tablet that was signed out or replaced would keep working for up to 15 minutes. Tokens without `crewSessionId` (staff, phone) are not affected. |

### 5.5 `Features/` — one folder per endpoint
| Folder | Endpoint | Notes |
|---|---|---|
| `StaffSignIn/` | `POST /auth/staff/sign-in` (Public) | `CredentialCheck`, then Controller / StationManager only. **Every failure is the same `401 UNAUTHENTICATED` message.** Five wrong passwords lock the account for 15 minutes (`429 SIGN_IN_LOCKED`, `Retry-After`). Refresh token in the cookie. |
| `CrewSignIn/` | `POST /auth/crew/sign-in` (Public) | Same, Driver only. Refresh token in the **body**, fixed 12 h. **No cab session**: the phone only reads the roster. |
| `CabSignIn/` | `POST /auth/crew/cab-sign-in` (Public) | `CabSignInValidator` (headers, fields, deviceTime within 24 h: `400`). `CabSignInHandler`: **the tablet first** (an unknown, deactivated or wrong-secret device is the same generic 401 and costs the driver nothing), then the driver (`CredentialCheck`), then in **one transaction**: end the driver's and the tablet's older sessions and revoke their refresh tokens, create the `CabSession` with the measured offset, make the refresh token (bound to the session), commit. Answers `{accessToken, expiresAt, refreshToken, refreshExpiresAt, crewSessionId, serverTime}`. Two sign-ins racing: the database allows one, the other gets `409 INVALID_STATE`. **The device headers and the password are never logged** (a test captures every log line at Trace level). |
| `RefreshSession/` | `POST /auth/refresh` (Public, the refresh token is the credential) | Rotation: each token works **once**; a replaced token may be used **once more within 10 seconds** (two tabs); any later or further use means a copy exists (threat T-14) and the whole session is revoked. Both steps are single `UPDATE … WHERE flag IS NULL` statements, so three simultaneous refreshes give two successes and one refusal. Staff: cookie, 30 min sliding. Crew: body, never past the fixed 12 h. A deactivated account is refused (within 15 min of deactivation). **A cab refresh also checks that the session is active and the tablet is still active** (a deactivated device is refused at its next refresh, threat T-12) and re-issues the same cab claims. A token presented in the wrong place (staff token in a body, crew token in a cookie) is refused without changing anything. |
| `SignOut/` | `POST /auth/sign-out` (Public, same credential and place as refresh) | Revokes the whole session (so a token still in the grace window cannot bring it back), **ends the cab session** if there is one (its access token stops at once), clears the cookie and answers **`204` always**, also with no or an unknown token. The access token itself is not revoked; it expires within 15 minutes. **For now the app calls sign-out after "End journey"**; later the `JourneyEnded` event (through the outbox) will end the session without the app's help. |
| `GetStaffMe/` | `GET /staff/me` (policy `Staff`) | Returns `{name, role, stationId}` for the caller; the account comes from the token's `sub`. `stationId` null = whole line. Also handy in tests: a driver is "authenticated but not Staff" (403), or "not authenticated" (401) once the session is over. |

### 5.6 `Authorization/` and `Problems.cs`
| File | What it does |
|---|---|
| `Policies.cs` | The policy names of contract §2: `Controller`, `StationManager`, `Staff` (either of those two), `CrewPhone` (Driver: a phone **or** a tablet), **`CrewCab`** (Driver whose token carries `crewSessionId`, so a phone token can never start a journey). Also sets the **fallback policy**: an endpoint that names no policy still needs a signed-in caller. Other modules write the same names as text, because they may not reference Identity's code. |
| `ProblemAuthorizationResultHandler.cs` | Makes a refused request answer as Problem Details with a code: `401 UNAUTHENTICATED` or `403 FORBIDDEN`. |
| `Problems.cs` | The module's error responses in the contract §6 shape, including `409 INVALID_STATE` for racing cab sign-ins. |

### 5.7 `Seed/` — staff accounts and cab devices from `shared/seed`
| File | What it does |
|---|---|
| `StaffSeedData.cs` | The shapes of `staff.json` and `cab-devices.json`. The files hold **no secrets**, only the *name* of the configuration key where each secret lives. |
| `IdentitySeedValidator.cs` | **Pure code.** Collects all problems (duplicate staff number, unknown role, station manager without station, unknown station code, missing secret, … and **a cab device whose `trainSetCode` the network does not have**). Messages never contain a secret value. |
| `IdentitySeeder.cs` | Validates, then writes in **one transaction**, finding each row by its natural key. Station codes become station IDs here, once (`IStationDirectory`); the train-set codes come from Network's `INetworkSchedule`, so a device registered to a train set that does not exist **stops the start** with a message naming both. Passwords and device secrets are hashed with Identity's `PasswordHasher` and re-hashed only when the stored hash no longer matches. |
| `IdentitySeedFolder.cs` | Finds `shared/seed` (a small copy of Network's finder: using Network's class would reach into its internals). |

### 5.8 `IdentityModule.cs`
`AddIdentityModule` registers: the `identity` DbContext; **ASP.NET Core Identity** (`AddIdentityCore`, lockout 5 failures / 15 min); `JwtSettings` and `AccessTokenIssuer`; **JWT bearer authentication** (signature, issuer, audience, only HS256, **no clock skew**, expiry judged on `TimeProvider`, and the cab-session check); the policies; the seeder and the startup service. `MapIdentityEndpoints` maps the seven endpoints. `IdentityStartupService` checks the signing key, prepares the dummy hashes, migrates (Development only), then seeds. **Call `AddIdentityModule` after `AddNetworkModule`**: the seed needs the stations and train sets.
The host (`Program.cs`) adds `UseAuthentication()` then `UseAuthorization()`; the order matters (who, then may they).
Network's public doors used here: `IStationDirectory` (station codes → IDs) and `INetworkSchedule` (train-set codes). The architecture test allows `Identity → Network` (approved 14 Oct).

### 5.9 Design decisions (one line each)
| Decision | Reason |
|---|---|
| Identity's `UserManager` + `PasswordHasher`; JWT issued by the backend | The risky parts (hashing, lockout) stay in the framework (ADR-10, CLAUDE.md §6) |
| Role as a column, not Identity role tables | One role per person; fewer tables to explain |
| Refresh token = random secret, only its hash stored, rotation with reuse detection | A leaked database cannot be used to sign in; a copy is noticed (T-14) |
| Staff: cookie, 30 min sliding. Crew: body, fixed 12 h | Browsers hide the cookie from scripts; apps have no cookie jar. A fixed shift limits a lost phone |
| Each refresh token is accepted from one place only | A staff token in a body, or a crew token in a cookie, can only mean a mistake or an attack |
| 10-second grace window, once | Two tabs sharing one cookie are normal; a copy used later is still caught |
| Dummy checks for unknown staff numbers and devices | Same work as a wrong password, so timing does not reveal what exists |
| One generic 401 for every sign-in failure, including the tablet's | No enumeration (contract §6, T-12); `DEVICE_NOT_REGISTERED` was removed (C-11) |
| The tablet is checked before the driver | A wrong tablet credential must never lock a real driver out |
| Identity owns the cab session (D-14); no session for phones | The token is only as good as its session; LiveOperations needs only the ids in the token |
| The cab session is checked on every request with `crewSessionId` | R-07 / R-19 must hold now, not after 15 minutes |
| One active cab session per driver **and per tablet**, enforced by the database | Two drivers must not both be "active" on one device |
| Clock offset measured once at sign-in, stored and carried in the token (R-09) | A wrong tablet clock must not make a train look early or late (T-17) |
| Station ID stored on the account at seeding | The token needs a UUID; asking Network once at seeding keeps sign-in fast |
| Fallback policy requires sign-in | A forgotten policy fails closed |
| HS256 with a symmetric key | Only this backend issues and checks tokens, so no key distribution is needed |

---

## 6. Tests

| Project | What it proves | Needs |
|---|---|---|
| `tests/Unit` | Seed rules (`Network/Seed/SeedValidator*Tests`), CSV splitting, the **real** `shared/seed` files; the journey rules: the four B-2 tests, UTC conversion and overnight stops (`LiveOperations/OperationalJourneyTests`), `JourneyPlannerTests` (Colombo date, exceptions, train set), `ServiceAssignmentsTests` | Nothing |
| `tests/Integration` | The real application on a real PostgreSQL: seed counts, `network` schema, restart does not duplicate, WRONG data stops startup, the ESTIMATE warning, the endpoint, the permission matrix; **journey generation** (12 journeys and 132 stops for the 3 days, UTC storage, a restart adds none, `ops` schema, the database refuses a duplicate, a wrong assignment file stops the start) | **Docker running** |
| `tests/Architecture` | Domain projects reference only `System.*`; no module depends on another except Disruptions → LiveOperations, LiveOperations → Network and Identity → Network | Nothing |
| `tests/Integration` → `OpenApiDocumentTests` | The running application's `/openapi/v1.yaml` equals the committed `backend/openapi.yaml`. An API change that is not reflected in the file fails the build (and CI). | Docker |
| `tests/Unit/Identity` | The access token (15 min, claims incl. the cab claims, no personal data), the refresh token (30 min, only the hash is stored, usable once, the 10 s replay rule, the fixed 12 h crew end and `Next`), the clock offset (R-09: sign, ±24 h limit), the staff / cab-device seed rules (incl. an unknown train set) | Nothing |
| `tests/Integration/Identity` | Seeding (8 accounts, no guard, station IDs, hashed passwords and device secrets, restart, missing secrets and an **unknown train set** stop the start); **staff sign-in**; **crew phone sign-in** (body tokens, fixed 12 h, no cab session, staff refused); **cab sign-in** (claims and stored offset, every credential problem the same generic 401, a bad tablet never locks a driver, headers and offset limit, R-19 per driver and per tablet, racing sign-ins, **no secret in any log line**); **refresh** (rotation, grace window, 30 min idle, sliding, crew body tokens, the fixed shift, each token only from its own place, cab refresh keeps its claims, deactivated device / driver); **the cab session checked on every request**; **sign-out** (cookie and body, ends the cab session); **timing** (one password check for an unknown number); **`/staff/me` and tokens** (expired, forged, unsigned, wrong issuer or audience, fallback policy, a positive control); `PolicyTests` (every policy × role incl. `CrewCab`) | **Docker running** |
| `tests/Unit/LiveOperations` (live) | The live rules in the aggregate (`OperationalJourneyLiveTests`, `StopSequenceTests`, `OverdueDelayTests`, `PositionRulesTests`), the outbox mapping (`Outbox/EventMapperTests`), the public projection, filter, hub, request parsing, resume decision and SSE framing (`Stream/*`), and **the shared event examples against the real shapes** (`Stream/EventExamplesTests`) | Nothing; the examples test reads `shared/events` |
| `tests/Integration/LiveOperations` | `OutboxTests` (rows in the same save; none on a failed save), `OutboxDispatcherTests` (order, sequence, retry and give-up, logs, retention), `StreamTests` (the public stream end to end), `StreamLatencyTests` (**G2**), plus the `/stream` rows of the permission matrix | **Docker running** |
| `tests/Contract` | (empty so far) | |

**Token tests must be able to fail.** On 15 Oct each check was broken on purpose (staff policy, fallback policy, signature, expiry, audience, issuer) and the matching test turned red every time, then the code was restored. Doing it exposed that the first forged-token tests could not fail: a forged token without `iat` / `nbf` gets the machine's real clock, which is a day ahead of the fixture's fake clock, so it was refused as "not valid yet" whatever the check did. The `Forge` helper now sets both, and the control test guards against it. On the same day the crew and cab tests were put through it: the device secret written to the log, an older session not ended, a deactivated device accepted, the session check removed, the 12-hour shift sliding. Each turned the matching tests red. Repeat the exercise when a check changes.

`ApiFixture` starts one PostgreSQL container and one copy of the application for all integration tests. The application's clock is a `FakeTimeProvider` fixed at **Wednesday 14 Oct 2026, 10:00 in Colombo**, so "today and the next 2 days" are three weekdays and the expected counts never depend on the real date. The journey tests read the tables with plain SQL, so they do not depend on the module's internals. Since 16 Oct the fixture also sets `Stream:HeartbeatSeconds` to 1 (the real default is 15), puts a `FaultInjectingPublisher` in front of the stream hub (a test can make one journey's hand-over fail) and captures the `OutboxPump` log. A test that needs a journey of its own makes one with `TestJourney` (the journey tables hold no foreign keys to Network, so stations can be made up); `SseClient` reads a stream the way the real clients do. **The permission matrix reads response headers only** (an open stream never ends). Run everything: `dotnet test backend/SmartRail.sln`.
**After changing an endpoint:** run the tests once with `UPDATE_OPENAPI=1` (PowerShell: `$env:UPDATE_OPENAPI="1"; dotnet test backend/SmartRail.sln; Remove-Item Env:UPDATE_OPENAPI`) to rewrite `backend/openapi.yaml`, then read `git diff backend/openapi.yaml` and compare it with the contract (CLAUDE.md §5.3).
`PermissionMatrixTests` holds one row per endpoint and role (Anonymous, Controller, StationManager, Driver = a crew **phone** token, DriverCab = a **cab session** token; every token comes from a real sign-in). Passenger, Display and Provider rows are added when those credentials exist. The tests make their own JWT signing key, demo password and cab-device secrets (`ApiFixture`), so nothing secret is committed and CI needs no user-secrets. A test that needs time to pass (token expiry, idle limit) starts its own copy of the application with its own `FakeTimeProvider`.

---

## 7. Running it locally

1. Copy `.env.example` to `.env`, set `POSTGRES_PASSWORD`, then `docker compose up -d`.
2. `dotnet user-secrets set "ConnectionStrings:SmartRail" "Host=localhost;Database=smartrail;Username=smartrail;Password=<your password>" --project backend/src/SmartRail.Api`
3. Set the secrets for staff sign-in (any values you like; the key needs 32+ characters; the demo password is the password of every seeded staff account):
   `dotnet user-secrets set "Jwt:SigningKey" "<32+ random characters>" --project backend/src/SmartRail.Api`
   `dotnet user-secrets set "Seed:DemoPassword" "<demo password>" --project backend/src/SmartRail.Api`
   `dotnet user-secrets set "Seed:CabDevice:CAB-01:Token" "<secret>" --project backend/src/SmartRail.Api` (and the same for `CAB-02`, `CAB-03`, `CAB-04`)
   Without them the application refuses to start and prints the exact command for the missing one.
4. `dotnet run --project backend/src/SmartRail.Api` — in Development it migrates and seeds, then serves `/api/v1/stations?query=kan`. Sign in as `CTRL-01` (controller) or `SM-FOT`, `SM-PLG`, `SM-KDT` (station managers) with the demo password; drivers `DRV-01` to `DRV-04` cannot sign in on the staff endpoint: they use `POST /auth/crew/sign-in` (phone) or `POST /auth/crew/cab-sign-in` with the headers `X-Device-Id: CAB-01` and `X-Device-Secret: <the secret you set for CAB-01>` (cab). Crew apps keep the refresh token from the response body and send it as `{"refreshToken": "…"}` to `/auth/refresh` and `/auth/sign-out`.

5. Optional settings (the value in brackets is the default): `Outbox:PollMilliseconds` (250) is how often saved events are looked for; `Stream:HeartbeatSeconds` (15) is the pause before an idle stream gets a comment line.
---

## 8. Open points

| # | Point | Status |
|---|---|---|
| 1 | `backend/openapi.yaml` | **Done (13 Oct):** YAML served by the app and compared in a test (the build-time generator can only write JSON, so that package was removed) |
| 2 | `MapOpenApi()` is public in all environments | Decide before release |
| 3 | Which end is coach position 1 | Undefined; no story needs it |
| 4 | Kandy distance is an ESTIMATE | Replace with the SLR Matale figure |
| 5 | Permission matrix has only the anonymous role | **Done (14 Oct)** for Anonymous, Controller, StationManager, Driver; add Passenger, Display, Provider with their credentials |
| 6 | The error schema in `openapi.yaml` must include `code`, `errors`, `requestId` (contract §6) | **Done (13 Oct)**; a test checks it |
| 7 | `openapi.yaml`: no `servers` entry, `query` required | **Done (13 Oct)**; tests check both |
| 8 | The `ProblemDetails` description says `code` and `requestId` are always present; that is true for our own errors, not for framework errors (for example a malformed request) | Watch when more endpoints exist |
| 9 | Journeys have **no driver yet**. `service-assignments.json` names one per service, but the journey does not carry it. **Deliberately not done in the crew sign-in task** (15 Oct, Senuja): another agent is changing `OperationalJourney` on `feature/live-journey`; add the driver after that branch is merged. (The guard is deferred: no guard in the seed files or the model) | Open, waiting for the merge |
| 10 | Generation reads **all** stored journey keys every hour | Fine for the MVP; revisit when journeys are archived |
| 11 | `TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo")` needs the time-zone database: present on Windows and on `ubuntu-latest`; a minimal container image would need `tzdata` | Check when a Docker image is built |
| 12 | Network's seed loader still creates ids with `Guid.CreateVersion7()`, which reads the system clock inside the call | Harmless (ids only); switch to the injected clock if a rule ever needs it |
| 13 | The domain model §10 does not list the four B-2 test names; the names were taken from the task | Add them to §10 if you want the document complete |
| 14 | **Unknown URLs answer 401 (not 404) to a caller who is not signed in**, because the default-deny policy applies to every request. Reveals nothing | Fine for the MVP; say if you prefer 404 |
| 15 | **Sign-out** (`POST /auth/sign-out`) | **Done (15 Oct).** The access token stays valid until it expires (at most 15 min) |
| 16 | Two simultaneous refreshes from one browser | **Done (15 Oct):** 10 s grace window, once. A third use, or a use after 10 s, still ends the session. A revoke that runs while another request is still creating its new token may leave that one token alive until it expires (30 min at most) |
| 17 | An unknown staff number answered faster than a wrong password | **Done (15 Oct):** `DummyPasswordCheck`. Proven by counting password checks, not by timing. The account-lock answer (429) still reveals a locked number exists: an **accepted risk** in the threat model §5 |
| 18 | Identity's `UserManager` reads the **system clock** for lock times, not our `TimeProvider`; `Retry-After` is therefore clamped to the lock length (15 min) | Tests cannot move lock expiry; harmless in production |
| 19 | Contract §3.3 says `/staff/me` returns "scope" without a shape; it returns `{name, role, stationId}` (`stationId` null = whole line). `openapi.yaml` cannot describe the refresh `Set-Cookie` header | Confirm the shape with the ops web |
| 20 | A cab device whose `trainSetCode` is not in the network | **Done (15 Oct):** the seed stops the start and names both |
| 21 | `IdentityUserContext` creates three empty tables (`AspNetUserClaims`, `…Logins`, `…Tokens`) | Leave: they are part of the documented setup |
| 22 | **End journey does not end the cab session yet.** The cab app calls sign-out after "End journey" for now. When LiveOperations has journeys that end, `JourneyEnded` (through the outbox) should end the session so the app's help is not needed | The outbox exists since 16 Oct, but nothing raises `JourneyEnded` yet; do it with the Start / End endpoints (Phase 2) |
| 23 | The clock offset ignores the network delay of the one sign-in request (milliseconds to a second or two) | Fine for minute-level delays (R-01) |
| 24 | A **phone** sign-in has no session record, so there is no list of "devices signed in" for a driver and no way to end one remotely except by deactivating the account | Fine for the MVP |
| 25 | Two cab sign-ins racing each other: the loser gets `409` and the app must retry | Mention in the cab app's sign-in screen |

| 26 | **Journeys generated before the `JourneyLineId` migration have an empty `LineId`**, so a `lineIds` filter never matches them (and their events carry an empty line). Generation never changes an existing journey (A-17) | Reset the development database, or backfill; no production data exists yet |
| 27 | **One dispatcher instance is assumed** (4.9) | Add a row lock and a shared fan-out before running two instances (architecture Q8) |
| 28 | **No cap on SSE connections per IP yet** (threat T-10, planned 17 Oct). A request is limited to 50 ids per list and one slow client is cut off, nothing more | Add with the other rate limits |
| 29 | **Contract §4 differences (not edited, for you to decide):** the contract names no `snapshot` event, but the stream sends one on connect and after `resync` (it says the client reloads its own snapshot, and the public journey endpoints do not exist yet); a request with **no filter** is a `400`; each list holds at most 50 ids; filters are alternatives (OR); `ops/stream` and `crew/stream` are not built | **Done (17 Oct):** contract §4 now says all of this (commit "Docs: contract section 4 …") |
| 30 | `causedBy` (actor, incident, request id) is **not in the envelope yet**: events from positions have no actor, and the interceptor must not depend on HTTP | Add with the decision endpoints (it is staff-only anyway) |
| 31 | A snapshot by station or line covers **today's** journeys and yesterday's that are still running (E-12); a journey asked for by id is returned whatever its date | Fine for the MVP |
| 32 | **A resume is refused (resync) more often than strictly needed** when sequence numbers have gaps (a failed dispatch uses one up) | Deliberate: a false resync costs one snapshot, a false resume would lose events |
| 33 | **CabSignInTests** `A_deactivated_device_is_the_generic_401_…` asserted *zero* active sessions for DRV-04, which only held when it ran before the tests that leave one; the full suite failed on this branch for that reason alone | **Fixed 16 Oct** (before / after comparison, like the clock-offset test next to it); revert it if you disagree |
| 34 | CLAUDE.md §3 and §4 still describe R-01 as "rounded down" and R-05 as the old rule | **Done (17 Oct):** CLAUDE.md §1, §4 now say "toward zero", the clarified R-05 and R-23 |
| 35 | `Stream/PublicProjection` lists three event types; every later event (JourneyStarted, TrainPositionUpdated, …) needs an allow-list entry **and** an example in `shared/events` in the same commit | A test fails if the example is missing |
| 36 | **Known gap, deferred (Senuja, 18 Oct): US-P4 AC2 "the displayed delay changes only after two consecutive updates" is not implemented.** The delay changes only at real stop events, minutes apart, so there is no rapid flicker; the "two consecutive updates" smoothing is deferred. What the code can do (checked with a throwaway test, not kept): consecutive stop events can differ by a minute, so `DelayChanged` can go 3 → 2 → 3 (arrival 3 late, departure 2 late after a short dwell, next arrival 3 late), and it can step down once after the R-23 estimate (0 → 3 → 5 → 4). GPS noise inside one stop cannot repeat events, and R-23 alone only grows | Open. No logic added; placement to decide before it is built |
| 37 | **Integration test classes are serial.** All 22 classes carry `[Collection("Api")]` and share one `ApiFixture`, so xUnit runs them one after another against the one database. Two of them start extra applications with their own clocks (`CrewSessionTests`, `RefreshSessionTests`); those applications also run journey generation and the outbox dispatcher against the same database. Harmless while their clocks move less than about 19 hours (nothing new to generate), but a test that advanced one further would add journeys and break the generation count test | Keep the clock advances small, or give such a test its own database |
| 38 | The ops-web typecheck, tests and build pass with the regenerated `schema.d.ts` (enums by name, strict numbers); `ProblemDetails.status` became a plain `number` | Done (18 Oct) |
---

## 9. Change log

| Date | Change |
|---|---|
| 13 Oct 2026 | First version: scaffold, Network module (model, migration, seed loader, `GET /stations`), tests |
| 13 Oct 2026 | OpenAPI: `ProblemDetails` gets `code`, `errors`, `requestId`; `query` required; no `servers` entry (three tests) |
| 14 Oct 2026 | US-B2: Network made internal with the public `INetworkSchedule`; LiveOperations gets `OperationalJourney` (Domain), the `ops` schema and migration, the journey planner, handler and hourly background service, the service-assignments check; architecture test allows LiveOperations → Network; FakeTimeProvider in the tests |
| 14 Oct 2026 | **Staff sign-in (US-O1, US-B7):** Identity module (`StaffAccount`, `CabDevice`, `RefreshToken`, `identity` schema and `InitialIdentity` migration, staff and cab-device seed with `IdentitySeedValidator`, `AccessTokenIssuer`, `RefreshTokens`, `POST /auth/staff/sign-in`, `POST /auth/refresh`, `GET /staff/me`, policies `Controller` / `StationManager` / `Staff` / `CrewPhone`, JWT bearer authentication, 401 / 403 as Problem Details); Network gains `IStationDirectory`; architecture test allows Identity → Network; `openapi.yaml` gets the Bearer scheme and three operations; test fixture generates its own secrets; permission matrix has roles |
| 15 Oct 2026 | **Sign-in follow-ups:** refresh grace window (10 s, once; `RefreshToken.ReplayedAt`, the `RefreshTokenReplayedAt` migration, `CanBeReplayedAt`); `POST /auth/sign-out` (`SignOut/`, `SessionRevocation`, `SessionCookie.Clear`); `DummyPasswordCheck` for unknown staff numbers; permission-matrix rows for sign-out; token tests proven able to fail (control test, issuer / audience / fallback tests, explicit `iat` / `nbf`); section 4.6 documents rules R-01 to R-06 from `develop`; `openapi.yaml` gets sign-out |
| 15 Oct 2026 | **Crew phone and cab sign-in (US-C1, US-C2, R-09, R-19):** `POST /auth/crew/sign-in`, `POST /auth/crew/cab-sign-in`; `CabSession` (Identity owns it, D-14) with database-enforced one-active-per-driver-and-per-tablet; `ClockOffset`; the cab session checked on every request (`CabSessionCheck`); refresh and sign-out accept the token in the body for crew and the cookie for staff, crew sessions end a fixed 12 h after sign-in, a cab refresh checks session and device; policy `CrewCab`; shared `CredentialCheck` and `PasswordSignInValidator` (`StaffSignInRequest` became `PasswordSignInRequest`); the seed validates cab devices' `trainSetCode` against Network; migration `CabSessions`; matrix role `DriverCab`; contract C-10 / C-11 (`DEVICE_NOT_REGISTERED` removed), domain model D-14. **The driver is not filled on journeys** (waiting for the `feature/live-journey` merge) |
| 14 Oct 2026 | Scope change, guard role deferred: guard accounts removed from `staff.json`, `guardStaffNumber` removed from `service-assignments.json` (the loader ignores that field, so no code change except a comment in `ServiceAssignments.cs`) |
| 13 Oct 2026 | `openapi.yaml`: served at `/openapi/v1.yaml` and checked by `OpenApiDocumentTests`; removed `Microsoft.Extensions.ApiDescription.Server` and the build-time workaround |
| 16 Oct 2026 | **Outbox and stream (Phase 1):** `LineId` on `ScheduledService` and the journey (migration `JourneyLineId`); `OutboxEvent`, `EventEnvelope`, `EventMapper`, `OutboxInterceptor` (migration `OutboxEvents`, the `EventSequence` sequence); `OutboxPump` and `OutboxDispatcher`; `PublicProjection`, `StreamHub`, `StreamHandler`, `SnapshotReader`, `GET /api/v1/stream` (Public; snapshot, resume, resync, heartbeat); `shared/events/*.json` and their tests; G2 latency test; permission-matrix rows; `openapi.yaml` regenerated; CLAUDE.md §9 changed (the outbox, dispatcher and stream live in LiveOperations); real timers for the poll and heartbeat as a deliberate exception to the TimeProvider rule |
| 16 Oct 2026 | **Domain (documented now):** the live state in `OperationalJourney` (4.7): R-08, R-09, R-23, inference of earlier stops, R-05 clarified, passed stops recorded; `LiveJourneyState` migration (enums as text) |
| 17 Oct 2026 | **Phase 2a, public read endpoints:** contract §3.1 shapes and §4 stream rows; `INetworkCatalog`; `GET /lines`, `GET /stations/{id}` (`IsCateringStation`, migration `StationCateringFlag`); `GET /journeys/search`, `GET /journeys/{id}` (`JourneyViews`, `ControlVersion`, migration `JourneyControlVersion`; `hold`, `reason`, `previousPlatform` null and `version` 0 until Phase 3); stream event schemas and the `StreamEvent` union in `openapi.yaml`; strict JSON numbers and enums by name; permission-matrix rows; CLAUDE.md wording for R-01, R-05, R-23 |
| 18 Oct 2026 | **Before merging Phase 2a:** search hides a train that has left the departure station only when the date searched is today; a `Date` header from the injected clock on every response (contract §1); `web/packages/api-client` `schema.d.ts` regenerated from `openapi.yaml` (after merging `develop` into the branch to get `web/`); open points 36 to 38 (anti-flicker analysis, serial test collection, web checks) |
