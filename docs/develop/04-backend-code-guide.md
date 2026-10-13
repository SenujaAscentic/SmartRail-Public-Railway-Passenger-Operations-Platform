# SmartRail – Backend Code Guide

**Written for:** Senuja, to read before a review (§28) and to explain the code without the AI.
**Kept up to date by:** the coding agent, after every change to backend code (CLAUDE.md §5, step 9).
**Last updated:** 13 Oct 2026 · **Covers:** solution scaffold, Network module (model, migration, seed, `GET /stations`), Api host, API description (`openapi.yaml`), tests.

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
| `SmartRail.Api` | The host: starts the program, connects the modules, serves HTTP | Network wired in |
| `SmartRail.Network` | The timetable data: stations, routes, services, train sets | **Built** (this guide) |
| `SmartRail.LiveOperations` / `.Domain` | Today's journeys and the delay/ETA rules | Empty |
| `SmartRail.Disruptions` / `.Domain` | Incidents | Empty |
| `SmartRail.Identity`, `.Notifications`, `.CateringIntegration` | Accounts, push, catering | Empty |
| `SmartRail.Simulator` | Replays GPS through the API | Empty |

---

## 2. The Api host

### `SmartRail.Api/Program.cs`
**What:** the first code that runs. It builds the web application and connects the modules.
**How:**
1. `AddNetworkModule(...)` registers the Network module's database and startup work.
2. `AddOpenApi()` / `MapOpenApi("/openapi/{documentName}.yaml")` make the application describe its own API at `/openapi/v1.yaml` (OpenAPI, as YAML). The committed copy is `backend/openapi.yaml`; see section 4. Two *transformers* adjust the description here, once for every module: one removes the `servers` entry (it would be copied from whichever request produced the file), and one adds `code`, `errors` and `requestId` to the `ProblemDetails` schema, because the framework only knows the standard fields and contract §6 says every error carries them. The error responses themselves are still built where the error happens.
3. `app.MapGroup("/api/v1")` creates the URL prefix every endpoint lives under (contract §1); `MapNetworkEndpoints()` puts the Network endpoints in it.
4. `public partial class Program;` at the bottom exists only so the integration tests can start the whole application in memory.

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

## 4. Tests

| Project | What it proves | Needs |
|---|---|---|
| `tests/Unit` | Seed rules (`Network/Seed/SeedValidator*Tests`), CSV splitting, and that the **real** `shared/seed` files pass every rule | Nothing |
| `tests/Integration` | The real application on a real PostgreSQL: seed counts, `network` schema, restart does not duplicate, WRONG data stops startup, the ESTIMATE warning, the endpoint, the permission matrix | **Docker running** |
| `tests/Architecture` | Domain projects reference only `System.*`; no module depends on another except Disruptions → LiveOperations | Nothing |
| `tests/Integration` → `OpenApiDocumentTests` | The running application's `/openapi/v1.yaml` equals the committed `backend/openapi.yaml`. An API change that is not reflected in the file fails the build (and CI). | Docker |
| `tests/Contract` | (empty so far) | |

`ApiFixture` starts one PostgreSQL container and one copy of the application for all integration tests. Run everything: `dotnet test backend/SmartRail.sln`.
**After changing an endpoint:** run the tests once with `UPDATE_OPENAPI=1` (PowerShell: `$env:UPDATE_OPENAPI="1"; dotnet test backend/SmartRail.sln; Remove-Item Env:UPDATE_OPENAPI`) to rewrite `backend/openapi.yaml`, then read `git diff backend/openapi.yaml` and compare it with the contract (CLAUDE.md §5.3).
`PermissionMatrixTests` holds one row per endpoint and role; only the anonymous role exists until sign-in is built.

---

## 5. Running it locally

1. Copy `.env.example` to `.env`, set `POSTGRES_PASSWORD`, then `docker compose up -d`.
2. `dotnet user-secrets set "ConnectionStrings:SmartRail" "Host=localhost;Database=smartrail;Username=smartrail;Password=<your password>" --project backend/src/SmartRail.Api`
3. `dotnet run --project backend/src/SmartRail.Api` — in Development it migrates and seeds, then serves `/api/v1/stations?query=kan`.

---

## 6. Open points

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

---

## 7. Change log

| Date | Change |
|---|---|
| 13 Oct 2026 | First version: scaffold, Network module (model, migration, seed loader, `GET /stations`), tests |
| 13 Oct 2026 | OpenAPI: `ProblemDetails` gets `code`, `errors`, `requestId`; `query` required; no `servers` entry (three tests) |
| 13 Oct 2026 | `openapi.yaml`: served at `/openapi/v1.yaml` and checked by `OpenApiDocumentTests`; removed `Microsoft.Extensions.ApiDescription.Server` and the build-time workaround |
