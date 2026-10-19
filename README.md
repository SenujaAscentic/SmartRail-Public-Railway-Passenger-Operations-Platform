# SmartRail

SmartRail is a live train-information system for one railway line (the Main Line, Colombo Fort to Kandy, both directions). A controller changes a train's delay, platform or status in the ops web, and passengers and station displays see it within seconds. A simulator plays the driver's cab tablet, so the whole chain can be shown without a real train.

This is an assessed MVP, demonstrated on 20 Oct 2026. What is built, and what is deliberately not, is listed under "What is deferred".

## Architecture

SmartRail is a **modular monolith**: one ASP.NET Core application (.NET 10) on PostgreSQL, split into modules that each own their own database schema and may only talk to each other through a public interface (a direct call) or a domain event (written to an outbox in the same transaction as the change, then sent on the live stream). An architecture test (`backend/tests/Architecture`) fails the build if a module references another module outside the allowed one-way dependencies, or if a Domain project references anything but System assemblies. The rules of the railway (delay, hold, platform change, source priority, ...) live only in the backend; the apps display what they receive. Live data reaches the apps as Server-Sent Events from `GET /api/v1/stream`. Full reasoning: [docs/develop/03-architecture-principles.md](docs/develop/03-architecture-principles.md).

| Module | Project (`backend/src/`) | State |
|---|---|---|
| Network | `SmartRail.Network` | Built: stations, lines, routes, timetable, seed data |
| LiveOperations | `SmartRail.LiveOperations` (+ `.Domain`) | Built: journeys, positions, controller decisions, board, outbox, the streams |
| Identity | `SmartRail.Identity` | Built: staff, crew and cab-device sign-in |
| Disruptions | `SmartRail.Disruptions` (+ `.Domain`) | Project only, no code yet |
| Notifications | `SmartRail.Notifications` | Project only, no code yet |
| CateringIntegration | `SmartRail.CateringIntegration` | Project only, no code yet (the RailBites provider service is not in the repository yet) |

The host is `SmartRail.Api`; `SmartRail.Simulator` is the cab tablet simulator.

## How to run it

Needs Docker, the .NET 10 SDK, Node 24 or newer, and Flutter (for the passenger app).

1. One-time setup: copy `.env.example` to `.env` and set `POSTGRES_PASSWORD`; set the user-secrets (connection string, `Jwt:SigningKey`, `Seed:DemoPassword`, the `Seed:CabDevice:CAB-0n:Token` values). The commands are in [section 7 of the backend code guide](docs/develop/04-backend-code-guide.md). Run `npm ci` in `web/`.
2. Demo, in PowerShell from the repository root:
   - `scripts/demo-reset.ps1` starts a fresh, empty database. **It deletes the local database.**
   - `scripts/demo-start.ps1 -ClockStart "2026-10-20T06:25:00+05:30"` opens the API (with the demo clock), the simulator for service 1001, the ops web and the station display, and prints the addresses. It asks for the cab secret and the demo password unless they are in the environment.
   - What the scripts do, and the manual steps behind them: [code guide, section 4.15](docs/develop/04-backend-code-guide.md) (the demo runbook).
3. The demo clock only runs in the Development environment. Search the date of that clock (20 Oct), not today's date.

Tests: `dotnet test --solution backend/SmartRail.sln` (needs Docker: the integration tests use a real PostgreSQL), `npm test` in `web/`, `flutter test` in `mobile/apps/passenger`.

## The apps

| App | Where | Address (demo) | Who it is for |
|---|---|---|---|
| API | `backend/src/SmartRail.Api` | http://localhost:5045 | all apps |
| Ops web | `web/apps/ops-web` | http://localhost:5173 | the controller |
| Departures board | `web/apps/station-display` | http://localhost:5174/board/FOT | passengers at a station (any station code, for example `/board/GPH`) |
| Platform display | `web/apps/station-display` | http://localhost:5174/platform/GPH/1 | passengers on a platform (`/platform/<station code>/<platform>`) |
| Passenger app | `mobile/apps/passenger` (Flutter) | `cd mobile/apps/passenger; flutter run` | passengers: search, journey details, live tracking |
| Simulator | `backend/src/SmartRail.Simulator` | console window | stands in for the driver's cab tablet |

Controller sign-in: staff number `CTRL-01` with the demo password you set in the user-secrets (station managers `SM-FOT`, `SM-PLG`, `SM-KDT` use the same password).

## The five end-to-end flows

Defined in [docs/qa/strategy.md, section 5](docs/qa/strategy.md). They are shown by hand for now: the Playwright automation (`e2e/`) is not built. Start the demo first (`demo-start.ps1`), which runs service 1001 (Fort to Kandy, 06:30 on the demo clock).

| # | Flow | How to show it | State |
|---|---|---|---|
| 1 | Search, then a live delay | In the passenger app search Colombo Fort to Kandy on 20 Oct and open service 1001. In the ops web open the journey and use **Delay**. The app shows "N min late" | Can be shown |
| 2 | The driver starts, the train moves | The simulator signs in as the cab, starts the journey and sends GPS. Watch the passenger app tracking screen, `/board/GPH` and `/platform/GPH/1` change as the train approaches | Can be shown |
| 3 | The controller cancels | In the ops web open the journey and use **Cancel journey**. `/board/FOT` shows "Cancelled"; the **Audit log** page lists the action | Cancel, board and audit can be shown; the push notification is deferred |
| 4 | Platform change with a hold | In the ops web use **Change platform** and **Hold** on a journey. The platform display of the old platform shows where the train departs from and "The train will wait for passengers at Platform N" | Displays and ops web can be shown; the crew instruction and its acknowledgement are deferred (driver app) |
| 5 | A delay reaches the catering provider | The delay produces an ETA event for the provider | Cannot be shown yet: the provider service and the catering integration are not built |

## What is deferred

The scope change of 19 Oct is the "Re-plan 2" row in the change log of [docs/define/moscow.md](docs/define/moscow.md). In short: the driver app, push notifications, crew messages, station announcements and add-stop / unskip are deferred, and the display pages use the public read-only board endpoint instead of display tokens. The catering integration was kept in scope but is not built yet (see the table above). The Notifications and Disruptions modules are projects without code. There is one controller role; no regions, interchanges, in-app food ordering or ticketing (MVP scope in [CLAUDE.md](CLAUDE.md)).

## Where the documents are

[docs/README.md](docs/README.md) is the full index. The folders:

| Folder | What is in it |
|---|---|
| `docs/define/` | What is built and why: [moscow.md](docs/define/moscow.md) (scope), [user-stories.md](docs/define/user-stories.md), [edge-cases.md](docs/define/edge-cases.md), personas and journeys |
| `docs/develop/` | How it is built: domain model and rules, API contract, architecture principles, technology decisions, delivery plan, and the [backend code guide](docs/develop/04-backend-code-guide.md) (file by file, with the demo runbook) |
| `docs/security/` | [threat-model.md](docs/security/threat-model.md): trust boundaries, threats and controls |
| `docs/qa/` | [strategy.md](docs/qa/strategy.md): test levels, the five E2E flows, defects |
| `docs/ai-usage/` | [ai-usage-log.md](docs/ai-usage/ai-usage-log.md): every use of AI, what was accepted, changed or rejected |

Also: `docs/design/` (design handoff and screens), `docs/discover/` (research), `shared/` (design tokens, status wording fixtures, event examples, seed data), and [CLAUDE.md](CLAUDE.md) (the rules the coding agent works under).
