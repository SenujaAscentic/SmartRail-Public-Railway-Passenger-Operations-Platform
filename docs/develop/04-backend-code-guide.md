# SmartRail – Backend Code Guide

**Written for:** Senuja, to read before a review (§28) and to explain the code without the AI.
**Kept up to date by:** the coding agent, after every change to backend code (CLAUDE.md §5, step 9).
**Last updated:** 14 Oct 2026 · **Covers:** solution scaffold, Network module (model, migration, seed, `GET /stations`, public schedule interface, `IStationDirectory`), LiveOperations (daily journey generation, US-B2), **Identity (staff sign-in, refresh, policies, staff and cab-device seed; US-O1, US-B7)**, Api host, API description (`openapi.yaml`), tests.

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
| `SmartRail.LiveOperations` / `.Domain` | Today's journeys and the delay/ETA rules | **Daily journey generation built** (section 4); no delay/ETA yet |
| `SmartRail.Disruptions` / `.Domain` | Incidents | Empty |
| `SmartRail.Identity` | Who is calling and may they do this: staff sign-in, tokens, policies | **Staff sign-in built** (section 5); cab-device sign-in, passengers, display devices, provider not yet |
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
6. `public partial class Program;` at the bottom exists only so the integration tests can start the whole application in memory.

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

Only **daily journey generation (US-B2)** exists so far: for today and the next 2 days, every service that runs on a date gets one *operational journey*, stored with all its stops. Delay, hold, tracking and the other statuses will come with their own rules.

```
 every hour (and at start) ► GenerateJourneysHandler
        reads the plan ◄── INetworkSchedule (Network)        reads trains ◄── service-assignments.json
                       └────────────► JourneyPlanner (pure: which journeys are missing?) ◄───────┘
                                              └► OperationalJourney.Plan (Domain: offsets → UTC) ► ops.Journeys, ops.JourneyStops
```

### 4.1 `LiveOperations.Domain/OperationalJourney.cs`
**What:** the aggregate: `Id`, `ServiceId`, `ServiceDate`, `TrainSetId`, `Status` (only `Planned`) and its `Stops` (sequence, station, Stop/Pass, scheduled arrival and departure in **UTC**, planned platform). The driver is not here yet (see open points); a journey will have a driver only, the guard role is deferred.
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

## 5. The Identity module (`SmartRail.Identity`)

Identity answers two questions: **who is calling?** (sign-in, tokens) and **may they do this?** (policies). Today it covers **staff** only (Controller, StationManager, Driver); passengers, display devices and the provider come later. The **guard role is deferred** (14 Oct, domain model D-12), so there are three roles.

```
POST /auth/staff/sign-in {staffNumber, password}
   ► validate fields (400) ► find account ► locked? (429) ► password wrong? count it (401, or 429 on the 5th)
   ► right password but Driver / deactivated? same 401 ► reset count
   ► issue ACCESS token (JWT, 15 min, in the body)  +  REFRESH token (random secret, 30 min idle, in an HttpOnly cookie)

every later request:  Authorization: Bearer <access token> ► JwtBearer checks signature + expiry ► the endpoint's policy checks the role

POST /auth/refresh (cookie only) ► look up the hash ► used/revoked before? => copied: revoke the whole session (401)
   ► expired (idle > 30 min)? 401 ► mark used (one atomic statement) ► new access token + new refresh cookie
```

### 5.1 `Model/` — the tables as C# classes (all `internal` except `StaffRole`)
| File | What it holds |
|---|---|
| `StaffRole.cs` | `Controller`, `StationManager`, `Driver`. Stored as its name. One role per person, so it is a column and Identity's role tables are not used. |
| `StaffAccount.cs` | A staff member: extends Identity's `IdentityUser<Guid>` (which gives the password hash, lockout counters and security stamp). `UserName` is the **staff number**. Adds `Name`, `Role`, `StationId` (station managers only) and `IsActive`. `MaySignInToOpsWeb` = active and Controller or StationManager; it is computed, so EF Core ignores it. |
| `CabDevice.cs` | A registered cab tablet: `Code` ("CAB-01"), `TrainSetCode` (text, the train set belongs to Network), `CredentialHash`, `Active`. **Identity owns this** (domain model D-13); only the hash of the secret is stored. Nothing uses it yet: cab sign-in is the next task. |
| `RefreshToken.cs` | One refresh token: `TokenHash` (never the secret), `FamilyId` (= one sign-in session), `ExpiresAt`, `UsedAt`, `RevokedAt`; `IsUsableAt(now)`. |

### 5.2 `Persistence/IdentityModuleDbContext.cs` and the `InitialIdentity` migration
**What:** the module's database access; all tables are in the PostgreSQL schema `identity`. It extends Identity's `IdentityUserContext`, which maps the user table (here `StaffAccounts`) and three tables this module never uses (`AspNetUserClaims`, `…Logins`, `…Tokens`); they stay empty.
**How/why:** a **check constraint** makes the database enforce "a station manager has a station, nobody else does" (domain model D-9). `CabDevice.Code` and `RefreshToken.TokenHash` have unique indexes (the seed finds devices by code; refresh finds tokens by hash). `Role` is stored as text like every enum.
**Watch out:** like the other modules, the migration applies automatically in Development only.

### 5.3 `Tokens/`
| File | What it does |
|---|---|
| `JwtSettings.cs` | Reads `Jwt:SigningKey` from configuration (user-secrets / environment, never the repo). Must be at least 32 characters, else the application will not start and the message gives the exact `dotnet user-secrets set` command. Issuer `smartrail` and audience `smartrail-api` are fixed names. |
| `AccessTokenIssuer.cs` | Creates the signed JWT (HS256). Claims: `sub` (account id), `role`, `stationId` (only for a station manager), plus `iss`, `aud`, `iat`, `nbf`, `exp`. **No name or other personal data.** Lifetime 15 minutes. Time comes from `TimeProvider`. |
| `RefreshTokens.cs` | `Create` makes 256 random bits as the secret and the row to store (only its SHA-256 hash). `IdleLifetime` = 30 minutes. A plain SHA-256 is right here because the secret is random; passwords and the cab-device secret use Identity's `PasswordHasher` instead. |
| `SessionCookie.cs` | Writes/reads the cookie `smartrail_refresh`: `HttpOnly` (scripts cannot read it), `Secure`, `SameSite=Strict`, path `/api/v1/auth` (only sent to the auth endpoints). |
| `AccessTokenResponse.cs` | The body of sign-in and refresh: `accessToken`, `expiresAt`. The refresh token is **never** in the body. |

### 5.4 `Features/` — one folder per endpoint
| Folder | Endpoint | Notes |
|---|---|---|
| `StaffSignIn/` | `POST /auth/staff/sign-in` (Public) | `StaffSignInValidator` (required fields, max 200 characters so nobody makes the server hash a huge password) ► `StaffSignInHandler`. **Every failure is the same `401 UNAUTHENTICATED` message** (unknown number, wrong password, a driver, a deactivated account). Five wrong passwords lock the account for 15 minutes (`429 SIGN_IN_LOCKED` with `Retry-After`); Identity's `UserManager` counts the failures. A successful sign-in resets the count. A new sign-in starts a new session family. |
| `RefreshSession/` | `POST /auth/refresh` (Public, cookie is the credential) | Rotation: each refresh token works **once**. A token that comes back after use or revoke means a copy exists (threat T-14), so the whole session is revoked. The "use it up" step is one SQL `UPDATE … WHERE UsedAt IS NULL`, so two refreshes at the same instant cannot both succeed. A deactivated account is refused here too (so within about 15 minutes of deactivation). Each refresh gives a fresh 30 minutes ("sliding"), so an active user stays signed in and an idle one is out after 30 minutes (US-O1). |
| `GetStaffMe/` | `GET /staff/me` (policy `Staff`) | Returns `{name, role, stationId}` for the caller; the account comes from the token's `sub`, never from the URL. `stationId` null = whole line. |

### 5.5 `Authorization/` and `Problems.cs`
| File | What it does |
|---|---|
| `Policies.cs` | The policy names of contract §2: `Controller`, `StationManager`, `Staff` (either of those two), `CrewPhone` (Driver). Also sets the **fallback policy**: an endpoint that names no policy still needs a signed-in caller, so a forgotten policy fails closed. Public endpoints say `AllowAnonymous()` explicitly. Other modules write the same names as text, because they may not reference Identity's code. |
| `ProblemAuthorizationResultHandler.cs` | Makes a refused request answer as Problem Details with a code: `401 UNAUTHENTICATED` (not signed in / token expired) or `403 FORBIDDEN` (wrong role). The framework's default sends an empty body. |
| `Problems.cs` | The module's error responses in the contract §6 shape (`type`, `title`, `code`, `requestId`, `errors`). |

### 5.6 `Seed/` — staff accounts and cab devices from `shared/seed`
| File | What it does |
|---|---|
| `StaffSeedData.cs` | The shapes of `staff.json` and `cab-devices.json`. The files hold **no secrets**, only the *name* of the configuration key where each secret lives. |
| `IdentitySeedValidator.cs` | **Pure code.** Collects all problems (duplicate staff number, unknown role, station manager without station, station code the network does not know, missing secret, …). Messages never contain a secret value; a missing secret's message gives the exact `dotnet user-secrets set` command. Tested in `tests/Unit/Identity`. |
| `IdentitySeeder.cs` | Validates, then writes in **one transaction**, finding each row by its natural key (staff number / device code), so a restart changes nothing unless the files or secrets changed. Station codes become **station IDs here, once**, via Network's public `IStationDirectory`. Passwords and device secrets are hashed with Identity's `PasswordHasher` and re-hashed only when the stored hash no longer matches the secret (so changing the demo-password secret takes effect at the next start). |
| `IdentitySeedFolder.cs` | Finds `shared/seed` (a small copy of Network's finder: using Network's class would reach into its internals). |

### 5.7 `IdentityModule.cs`
`AddIdentityModule` registers: the `identity` DbContext; **ASP.NET Core Identity** (`AddIdentityCore`, lockout 5 failures / 15 min); `JwtSettings` and `AccessTokenIssuer`; **JWT bearer authentication** (signature, issuer, audience, only HS256, **no clock skew** so 15 minutes means 15 minutes, and expiry judged on `TimeProvider`); the policies; the seeder and the startup service. `MapIdentityEndpoints` maps the three endpoints. `IdentityStartupService` checks the signing key, migrates (Development only), then seeds. **Call `AddIdentityModule` after `AddNetworkModule`**: the seed needs the stations.
The host (`Program.cs`) adds `UseAuthentication()` then `UseAuthorization()`; the order matters (who, then may they).
Network gained one public door for this: `IStationDirectory` (station codes → IDs). The architecture test now allows `Identity → Network` (approved 14 Oct).

### 5.8 Design decisions (one line each)
| Decision | Reason |
|---|---|
| Identity's `UserManager` + `PasswordHasher`; JWT issued by the backend | The risky parts (hashing, lockout) stay in the framework (ADR-10, CLAUDE.md §6) |
| Role as a column, not Identity role tables | One role per person; fewer tables to explain |
| Refresh token = random secret, only its hash stored, rotation with reuse detection | A leaked database cannot be used to sign in; a copied cookie is noticed (T-14) |
| Refresh token in an HttpOnly cookie, access token in the body | Scripts cannot steal the long-lived token (C-3, T-14) |
| 30-minute sliding refresh expiry | This *is* the 30-minute idle rule (US-O1), with no extra bookkeeping |
| One generic 401 for every sign-in failure | No account enumeration (contract §6) |
| Station ID stored on the account at seeding | The token needs a UUID; asking Network once at seeding keeps sign-in fast and the dependency tiny |
| Fallback policy requires sign-in | A forgotten policy fails closed |
| HS256 with a symmetric key | Only this backend issues and checks tokens, so no key distribution is needed |
| Device secret hashed with `PasswordHasher` | "No custom hashing"; the cost only matters at cab sign-in, not per request |

---

## 6. Tests

| Project | What it proves | Needs |
|---|---|---|
| `tests/Unit` | Seed rules (`Network/Seed/SeedValidator*Tests`), CSV splitting, the **real** `shared/seed` files; the journey rules: the four B-2 tests, UTC conversion and overnight stops (`LiveOperations/OperationalJourneyTests`), `JourneyPlannerTests` (Colombo date, exceptions, train set), `ServiceAssignmentsTests` | Nothing |
| `tests/Integration` | The real application on a real PostgreSQL: seed counts, `network` schema, restart does not duplicate, WRONG data stops startup, the ESTIMATE warning, the endpoint, the permission matrix; **journey generation** (12 journeys and 132 stops for the 3 days, UTC storage, a restart adds none, `ops` schema, the database refuses a duplicate, a wrong assignment file stops the start) | **Docker running** |
| `tests/Architecture` | Domain projects reference only `System.*`; no module depends on another except Disruptions → LiveOperations, LiveOperations → Network and Identity → Network | Nothing |
| `tests/Integration` → `OpenApiDocumentTests` | The running application's `/openapi/v1.yaml` equals the committed `backend/openapi.yaml`. An API change that is not reflected in the file fails the build (and CI). | Docker |
| `tests/Unit/Identity` | The access token (15 min, claims, no personal data), the refresh token (30 min, only the hash is stored, usable once), the staff / cab-device seed rules | Nothing |
| `tests/Integration/Identity` | Seeding (8 accounts, no guard, station IDs, hashed passwords and device secrets, restart, missing secrets stop the start); **sign-in** (generic 401, lockout after 5, driver refused, cookie flags, field errors); **refresh** (rotation, reuse revokes the session, 30 min idle, sliding, deactivated account, simultaneous refresh); **`/staff/me` and tokens** (expired, forged, unsigned, 401 / 403 bodies); `PolicyTests` (every policy × role, and every endpoint declares who may call it) | **Docker running** |
| `tests/Contract` | (empty so far) | |

`ApiFixture` starts one PostgreSQL container and one copy of the application for all integration tests. The application's clock is a `FakeTimeProvider` fixed at **Wednesday 14 Oct 2026, 10:00 in Colombo**, so "today and the next 2 days" are three weekdays and the expected counts never depend on the real date. The journey tests read the tables with plain SQL, so they do not depend on the module's internals. Run everything: `dotnet test backend/SmartRail.sln`.
**After changing an endpoint:** run the tests once with `UPDATE_OPENAPI=1` (PowerShell: `$env:UPDATE_OPENAPI="1"; dotnet test backend/SmartRail.sln; Remove-Item Env:UPDATE_OPENAPI`) to rewrite `backend/openapi.yaml`, then read `git diff backend/openapi.yaml` and compare it with the contract (CLAUDE.md §5.3).
`PermissionMatrixTests` holds one row per endpoint and role (Anonymous, Controller, StationManager, Driver). Passenger, Display and Provider rows are added when those credentials exist. The tests make their own JWT signing key, demo password and cab-device secrets (`ApiFixture`), so nothing secret is committed and CI needs no user-secrets. A test that needs time to pass (token expiry, idle limit) starts its own copy of the application with its own `FakeTimeProvider`.

---

## 7. Running it locally

1. Copy `.env.example` to `.env`, set `POSTGRES_PASSWORD`, then `docker compose up -d`.
2. `dotnet user-secrets set "ConnectionStrings:SmartRail" "Host=localhost;Database=smartrail;Username=smartrail;Password=<your password>" --project backend/src/SmartRail.Api`
3. Set the secrets for staff sign-in (any values you like; the key needs 32+ characters; the demo password is the password of every seeded staff account):
   `dotnet user-secrets set "Jwt:SigningKey" "<32+ random characters>" --project backend/src/SmartRail.Api`
   `dotnet user-secrets set "Seed:DemoPassword" "<demo password>" --project backend/src/SmartRail.Api`
   `dotnet user-secrets set "Seed:CabDevice:CAB-01:Token" "<secret>" --project backend/src/SmartRail.Api` (and the same for `CAB-02`, `CAB-03`, `CAB-04`)
   Without them the application refuses to start and prints the exact command for the missing one.
4. `dotnet run --project backend/src/SmartRail.Api` — in Development it migrates and seeds, then serves `/api/v1/stations?query=kan`. Sign in as `CTRL-01` (controller) or `SM-FOT`, `SM-PLG`, `SM-KDT` (station managers) with the demo password; drivers `DRV-01` to `DRV-04` cannot sign in on the staff endpoint.

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
| 9 | Journeys have **no driver yet**. `service-assignments.json` names one per service, but the journey does not carry it. Fill it after the sign-in branch is merged; until then nothing is invented. (The guard is deferred: 14 Oct, no guard in the seed files or the model) | Open |
| 10 | Generation reads **all** stored journey keys every hour | Fine for the MVP; revisit when journeys are archived |
| 11 | `TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo")` needs the time-zone database: present on Windows and on `ubuntu-latest`; a minimal container image would need `tzdata` | Check when a Docker image is built |
| 12 | Network's seed loader still creates ids with `Guid.CreateVersion7()`, which reads the system clock inside the call | Harmless (ids only); switch to the injected clock if a rule ever needs it |
| 13 | The domain model §10 does not list the four B-2 test names; the names were taken from the task | Add them to §10 if you want the document complete |
| 14 | **Unknown URLs answer 401 (not 404) to a caller who is not signed in**, because the default-deny policy applies to every request. Reveals nothing | Fine for the MVP; say if you prefer 404 |
| 15 | **Sign-out** (`POST /auth/sign-out`, contract §3.2) is not built, so a session ends only by 30 min idle, refresh reuse or deactivation | Add with the ops web sign-out button |
| 16 | Two **simultaneous refreshes** from one browser (two tabs) look like a copied token and end the session; the winner's new token may survive a revoke that runs a moment later | Acceptable for the MVP; the web app should refresh from one place |
| 17 | An **unknown staff number answers faster** than a wrong password (no hashing happens), which could reveal that a number exists | Low risk with seeded accounts; equalise later if wanted |
| 18 | Identity's `UserManager` reads the **system clock** for lock times, not our `TimeProvider`; `Retry-After` is therefore clamped to the lock length (15 min) | Tests cannot move lock expiry; harmless in production |
| 19 | Contract §3.3 says `/staff/me` returns "scope" without a shape; it returns `{name, role, stationId}` (`stationId` null = whole line). `openapi.yaml` cannot describe the refresh `Set-Cookie` header | Confirm the shape with the ops web |
| 20 | The contract does not say the cab-device `trainSetCode` is checked against the network; the seed does not check it | Decide with cab sign-in (next task) |
| 21 | `IdentityUserContext` creates three empty tables (`AspNetUserClaims`, `…Logins`, `…Tokens`) | Leave: they are part of the documented setup |

---

## 9. Change log

| Date | Change |
|---|---|
| 13 Oct 2026 | First version: scaffold, Network module (model, migration, seed loader, `GET /stations`), tests |
| 13 Oct 2026 | OpenAPI: `ProblemDetails` gets `code`, `errors`, `requestId`; `query` required; no `servers` entry (three tests) |
| 14 Oct 2026 | US-B2: Network made internal with the public `INetworkSchedule`; LiveOperations gets `OperationalJourney` (Domain), the `ops` schema and migration, the journey planner, handler and hourly background service, the service-assignments check; architecture test allows LiveOperations → Network; FakeTimeProvider in the tests |
| 14 Oct 2026 | **Staff sign-in (US-O1, US-B7):** Identity module (`StaffAccount`, `CabDevice`, `RefreshToken`, `identity` schema and `InitialIdentity` migration, staff and cab-device seed with `IdentitySeedValidator`, `AccessTokenIssuer`, `RefreshTokens`, `POST /auth/staff/sign-in`, `POST /auth/refresh`, `GET /staff/me`, policies `Controller` / `StationManager` / `Staff` / `CrewPhone`, JWT bearer authentication, 401 / 403 as Problem Details); Network gains `IStationDirectory`; architecture test allows Identity → Network; `openapi.yaml` gets the Bearer scheme and three operations; test fixture generates its own secrets; permission matrix has roles |
| 14 Oct 2026 | Scope change, guard role deferred: guard accounts removed from `staff.json`, `guardStaffNumber` removed from `service-assignments.json` (the loader ignores that field, so no code change except a comment in `ServiceAssignments.cs`) |
| 13 Oct 2026 | `openapi.yaml`: served at `/openapi/v1.yaml` and checked by `OpenApiDocumentTests`; removed `Microsoft.Extensions.ApiDescription.Server` and the build-time workaround |
