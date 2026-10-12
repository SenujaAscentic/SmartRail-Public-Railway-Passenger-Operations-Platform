# SmartRail – API Contract (design-first)

**Phase:** Develop · **Status:** Agreed 12 Oct 2026 (C-1–C-7) + C-8 · **Owner:** Senuja
**Approach (ADR-09, option B):** this document is the agreed design. The machine-readable `backend/openapi.yaml` is **generated from the code**, committed, and checked in CI; any difference from this document is resolved here first. TypeScript and Dart clients are generated from `openapi.yaml`.
**Builds on:** `01-domain-model.md` (rules R-xx, events, decisions D-x), `03-architecture-principles.md`, `define/user-stories.md`.

Decisions are collected in section 9.

---

## 1. Conventions

| Topic | Rule |
|---|---|
| Base path | `/api/v1`. Breaking changes need `/v2`; adding fields is not breaking (clients ignore unknown fields). |
| Format | JSON, `camelCase`. Enums as strings (`"Cancelled"`, not `3`). |
| Times | ISO 8601 in **UTC** (`2026-10-14T04:40:00Z`); apps format in Asia/Colombo (C-1). Service dates as `YYYY-MM-DD`. |
| IDs | UUIDs. Service numbers (`"1005"`) and staff numbers are text, never used as IDs in URLs. |
| Status values | The API sends **structured** status (`kind`, `delayMinutes`, `previousPlatform`, …); every app formats the text with the shared `status-fixtures.json` tests (C-4). |
| Lists | Small lists are returned whole. Long lists (audit log, alerts history) use `?limit=&cursor=`; the response has `nextCursor`. |
| Concurrency | Every **decision** on a journey sends `expectedVersion` = the journey's **control version** (bumped only by decisions, not by GPS, detection or crew taps); a stale version is refused with `409 VERSION_CONFLICT` and the latest journey (R-18, C-2). Crew and position endpoints never send it. |
| Idempotency | Crew actions and position batches carry a client-generated `clientActionId` (UUID); a repeat is answered `200` with the original result and changes nothing (C-7). |
| Rate limits | Per credential and per IP; `429` with `Retry-After`. Stricter on sign-in (US-P5: 5 failures → 15-min lock). |
| Errors | RFC 9457 Problem Details (section 6). |
| Personal data | Never in URLs or query strings (Section 17/20); never in logs. |

---

## 2. Audiences, credentials and policies (authentication design)

| Audience | How it signs in | Token | Lifetime | Policy name |
|---|---|---|---|---|
| **Public** (anyone) | – | – | – | `Public` |
| **Passenger** (app) | email + password | Access token (JWT) + refresh token in the phone's secure storage | Access 15 min; refresh 30 days **sliding**: every refresh issues a new token with a fresh 30 days, so only 30 days of inactivity requires signing in again; "stay signed in" (US-P5). All sessions end on sign-out, password change, account deletion or refresh-token reuse (T-14) | `Passenger` |
| **Staff** (ops web) | staff number + password | Access token held in memory; refresh token in an `HttpOnly; Secure; SameSite=Strict` cookie (C-3) | Access 15 min; session ends after **30 min idle** (US-O1) | `Controller` (whole line), `StationManager` (own station) |
| **Crew, phone mode** | staff number + password | Access + refresh token | Shift-length refresh (12 h) | `CrewPhone` (Driver, Guard) |
| **Crew, cab mode** | **device credential** (seeded per tablet) **+** staff sign-in | Cab session token bound to device + staff + session; carries the measured clock offset | Until End journey / sign-out / new cab sign-in elsewhere (R-19) | `CrewCab` (Driver) |
| **Display device** | device credential (seeded) | Read-only token, `stationId` claim | 24 h, renewed automatically | `Display` |
| **Catering provider** (calling SmartRail) | API key | – | Rotatable | `Provider` |

**Token claims (JWT):** `sub`, `aud`, `role`, `stationId?`, `deviceId?`, `crewSessionId?`, `exp`. Signed by the backend; no personal data inside.

**Scope checks** happen in the handler, not only by role: a station manager posting to another station, or a driver acting on a journey not assigned to them, gets `403 OUT_OF_SCOPE` (R-17), and the refusal is logged (US-O1).

**SSE and tokens:** the browser's built-in `EventSource` cannot send an `Authorization` header, and putting tokens in the URL would leak them into logs. Web apps therefore read the stream with a fetch-based SSE client (e.g. `@microsoft/fetch-event-source`) that sends the header; Flutter sends it directly.

---

## 3. Endpoint catalogue

Notation: **Who** = policy; **Story** = main story served.

### 3.1 Public network and live information (no sign-in)

| Method · Path | Who | Purpose | Story |
|---|---|---|---|
| `GET /stations?query=kan` | Public | Type-ahead, max 10 | US-P1 |
| `GET /stations/{stationId}` | Public | Station with platforms, catering flag | US-P1, P-8 |
| `GET /lines` | Public | Lines with ordered stations and distances (line view, line diagram) | US-P2, US-O2 |
| `GET /journeys/search?from=&to=&date=&after=&limit=` | Public | Direct journeys that **stop** at both, origin first, run on date, sorted by departure. `after` defaults to **now** for today (past departures hidden) and to start of day for other dates; `limit` optional. Cancelled journeys are **included with their status**. Also used for "next trains" after a missed or cancelled train (`after` = its departure, `limit=3`) and for alternatives after early termination (`from` = termination station). `fares[]` per class (Should P-9) | US-P1, P-3, P-4 |
| `GET /journeys/{journeyId}` | Public | Journey detail: stops (scheduled / expected / actual, platform + previous, status, source), today's train set (traction, coaches), tracking freshness, last position, hold, reason, `version` | US-P2, P-3, P-4, C-3 |
| `GET /stations/{stationId}/board?kind=departures\|arrivals&limit=10` | Public | Board rows | US-D1, D-3 |
| `GET /stations/{stationId}/platforms/{platform}/next?count=3` | Public | Next train + remaining stops + following two; non-stopping flag; redirect notice | US-D2 |
| `GET /stations/{stationId}/timetable?date=` | Public | Whole day's scheduled departures for offline cache | US-D4 |
| `GET /stations/{stationId}/announcements` | Public | Active announcements, by priority | US-D3 |
| `GET /stations/{stationId}/catering` | Public | Cached menu, hotline, opening hours, `available`; `"unavailable"` state if provider down | US-P8, F-3 |
| `GET /notices/network` | Public | Network-wide incident banner (Should S-4) | S-4 |
| `GET /stream?journeyIds=&stationIds=` | Public | **Public** event stream (section 4) | US-B5 |

### 3.2 Passenger account

| Method · Path | Who | Purpose | Story |
|---|---|---|---|
| `POST /auth/passengers/register` | Public | `{name, email, phone, password}` → tokens; field-specific validation messages (▶ C-7) | US-P5 |
| `POST /auth/passengers/sign-in` | Public | `{email, password}` → tokens; one generic error; lock after 5 failures | US-P5 |
| `POST /auth/refresh` · `POST /auth/sign-out` | Passenger / staff / crew | Rotate refresh token · revoke it | US-P5, O-1 |
| `GET /me` · `DELETE /me` | Passenger | Profile · delete = anonymise + remove saved trips + push tokens | US-P5 |
| `GET /me/saved-trips` · `POST /me/saved-trips` · `DELETE /me/saved-trips/{id}` | Passenger | Max 5; GET includes **today's** status per trip and "does not run today" | US-P6 |
| `PUT /me/push-subscription` · `DELETE /me/push-subscription` | Passenger | `{fcmToken, platform}` | US-P7 |

### 3.3 Staff and operations (ops web)

| Method · Path | Who | Purpose | Story |
|---|---|---|---|
| `POST /auth/staff/sign-in` | Public | `{staffNumber, password}` | US-O1 |
| `GET /staff/me` | Staff | Name, role, scope (for UI defaults) | US-O1 |
| `GET /ops/dashboard?stationId=&fromStationId=&toStationId=` | Controller | Alerts (severity, newest first), disrupted journeys by lateness, running journeys for line view, provider health | US-O2, O-3, F-4 |
| `GET /ops/journeys/{journeyId}` | Controller | Journey detail **plus** staff-only fields: source per value, override flags, GPS-contradicts-override warning, crew, messages | US-O3, O-4 |
| `POST /ops/journeys/{id}/delay` | Controller | `{minutes, reasonCode, note?, expectedVersion}` → sets a controller delay (R-05) | US-O4 |
| `DELETE /ops/journeys/{id}/delay` | Controller | Clear the controller override; GPS / crew values apply again | US-O3 |
| `POST /ops/journeys/{id}/hold` · `POST …/hold/release` | Controller | `{stationId, minutes, reason, expectedVersion}` (R-22) | US-O4 |
| `POST /ops/journeys/{id}/cancel` · `POST …/reinstate` | Controller | `{reasonCode, note?, incidentId?, expectedVersion}`; reinstate only before scheduled start (R-14) | US-O4 |
| `POST /ops/journeys/{id}/terminate` | Controller | `{atStationId, reasonCode, expectedVersion}` | US-O4 |
| `POST /ops/journeys/{id}/stops/{stationId}/skip` · `…/unskip` | Controller | R-15, R-21, D-6 | US-O4 |
| `POST /ops/journeys/{id}/stops` | Controller | Add stop `{stationId, platform, expectedArrival?, expectedVersion}`; time estimated if omitted (D-5) | US-O4 |
| `POST /ops/journeys/{id}/impact-preview` | Controller | `{action, …}` → what will be affected (stations, displays, catering, saved-trip passengers count) **without changing anything**; feeds the confirmation dialog | US-O4 |
| `POST /ops/journeys/{id}/stops/{stationId}/platform/preview` | Controller | `{platform}` → `leadTimeMinutes`, `leadTimeLevel`, `holdRecommended`, `conflicts[]` (R-11, R-12); changes nothing (C-5) | US-O5 |
| `POST /ops/journeys/{id}/stops/{stationId}/platform` | Controller | `{platform, setHold, holdMinutes?, conflictsAcknowledged, expectedVersion}`; refused `422 CONFLICT_NOT_ACKNOWLEDGED` if a conflict exists and was not acknowledged | US-O5 |
| `PUT /ops/journeys/{id}/stops/{stationId}/times` | Controller | Correct actual arrival / departure (source = Controller) | US-O3 |
| `GET /ops/journeys/{id}/messages` · `POST /ops/journeys/{id}/messages` | Controller | Send `{kind: Info\|Instruction, templateId?, text}`; per-recipient status sent / delivered / acknowledged | US-O7 |
| `GET /ops/crew-reports?status=New` · `POST /ops/crew-reports/{id}/dismiss` | Controller | Dismiss with `{reason}` | US-O6 |
| `POST /ops/incidents` | Controller | `{type, description, location, scope, fromReportId?, effects:[{journeyId, action, params, expectedVersion}]}` → incident **and** all effects in **one transaction** (architecture Q3); any refused effect rolls back everything and returns which one failed | US-O6, O-4 |
| `POST /ops/incidents/{id}/resolve` | Controller | Close incident; banner clears | S-4 |
| `GET /ops/alerts` · `POST /ops/alerts/{id}/acknowledge` | Controller | Unscheduled stop, stale / lost data, unacknowledged instruction, provider unreachable | US-O2, O-8 |
| `GET /ops/audit?journeyId=&actorId=&from=&to=&cursor=` | Controller | Read-only audit log | US-O10 |
| `GET /stations/{stationId}/announcements/manage` · `POST …/announcements` · `DELETE …/announcements/{id}` | StationManager (own station) | `{text ≤ 200, priority, validFrom, validTo}`; plain text only | US-O9 |

### 3.4 Crew app

| Method · Path | Who | Purpose | Story |
|---|---|---|---|
| `POST /auth/crew/sign-in` | Public | Phone mode: `{staffNumber, password}` | US-C1 |
| `POST /auth/crew/cab-sign-in` | Device credential (header) | Cab mode: `{staffNumber, password, deviceTime}` → cab session token + `serverTime` (clock offset measured here, R-09); ends any previous cab session of this driver (R-19) | US-C2 |
| `GET /crew/roster?date=` | CrewPhone, CrewCab | Today's assigned journeys in order: train set, times, route, departure platform | US-C1 |
| `POST /crew/journeys/{id}/start` · `POST …/end` | **CrewCab only** | Start from 60 min before departure, assigned driver only; train set mismatch → warning in response + controller alert (Should S-11) | US-C2 |
| `POST /crew/journeys/{id}/positions` | **CrewCab only** | Batch `[{lat, lng, speedKmh, accuracyM, gpsTime?, deviceTime}]` + `clientActionId`; plausibility rules R-07, R-08 | US-B3 |
| `POST /crew/journeys/{id}/stops/{stationId}/arrived` · `…/departed` | CrewCab, CrewPhone (guard) | `{occurredAt, clientActionId}`; source = Crew (R-05, R-20) | US-C4, C-7 |
| `POST /crew/journeys/{id}/reports` | CrewCab, CrewPhone | `{type, note? ≤ 200, occurredAt, location?, clientActionId}` | US-C5 |
| `GET /crew/journeys/{id}/messages` · `POST /crew/messages/{id}/acknowledge` | CrewCab, CrewPhone | Acknowledge `{clientActionId}`; marks delivered on fetch | US-C6 |
| `GET /crew/stream` | CrewCab, CrewPhone | Crew stream: own journeys' public events + instructions | US-C6 |

The **simulator** signs in as a seeded cab device and uses exactly these endpoints (US-B3).

### 3.5 Display devices

| Method · Path | Who | Purpose | Story |
|---|---|---|---|
| `POST /auth/devices/display/token` | Device credential | Read-only token for its station | US-D4 |
| `POST /devices/heartbeat` | Display, CrewCab | `{appVersion, lastEventId, online}` for operations monitoring | §19 |
| *(all reads)* | Display | Use the public endpoints in 3.1 and `GET /stream?stationIds=` | US-D1–D4 |

Every write endpoint refuses a display token (`403`), tested in the permission matrix (US-B7).

### 3.6 Catering provider integration

| Direction | Method · Path | Auth | Purpose | Story |
|---|---|---|---|---|
| Provider → SmartRail | `GET /api/v1/provider/journeys?date=` | `Provider` API key | Journeys calling at the provider's stations that day, with scheduled and expected times (no passenger data) | US-F1 |
| SmartRail → provider | `GET {provider}/provider-api/v1/stations` · `GET …/stations/{ref}/menu` | SmartRail's API key at the provider | Stations, menus, availability, hotline, opening hours; fetched every 5 min and cached | US-F3 |
| SmartRail → provider | `POST {provider}/provider-api/v1/webhooks/smartrail` | HMAC signature (domain model 8.3) | `provider.eta-updated`, `provider.journey-changed` | US-F2 |
| Provider (own UI) | `GET {provider}/events` | – (demo page) | Event log page | US-F1 |

**Retry schedule** for webhooks (from the outbox): 10 s, 30 s, 1 min, 5 min, then every 15 min for 24 h. The provider must answer `2xx` within 5 s; a duplicate `eventId` is answered `200`. Undelivered > 5 min → `ProviderHealthChanged` and an ops alert (US-F4).

### 3.7 Operations and development

| Method · Path | Purpose |
|---|---|
| `GET /health/live` · `GET /health/ready` | Liveness (process up) · readiness (database, outbox lag, provider reachability as "degraded" not "down") |
| `POST /dev/reset` · `POST /dev/simulator/scenarios/{name}` | Reset demo data · start a simulator scenario. **Registered only in the Development environment**; absent in any other build |

---

## 4. Event stream (SSE)

| Topic | Rule |
|---|---|
| Endpoints | `GET /stream` (public), `GET /ops/stream` (Controller, StationManager), `GET /crew/stream` (crew). Same format; different events and filters. |
| Filters | `journeyIds`, `stationIds`, `lineIds` (comma-separated). Display: its station. Controller: whole line. |
| Message format | `id: <sequence>` · `event: <EventType>` · `data: <JSON envelope + payload>` (domain model 8.1–8.2). |
| Heartbeat | Comment line every 15 s, so proxies keep the connection open and clients can detect silence. |
| Resume | Client sends `Last-Event-ID`; the server replays missed events if still retained (last 24 h); otherwise sends `event: resync` and the client reloads its snapshot (journey or board) before continuing. |
| Ordering | Sequence numbers increase; a client ignores an event with a lower sequence than its snapshot's. |

**Who sees which events:**

| Event | Public | Ops | Crew (own journeys) |
|---|---|---|---|
| JourneyStarted, JourneyEnded, TrainPositionUpdated, StopStatusChanged, DelayChanged, DataFreshnessChanged, JourneyHeld, PlatformChanged, JourneyCancelled, JourneyTerminatedEarly, StopSkipped, StopAdded, AnnouncementPosted | ✓ | ✓ | ✓ |
| IncidentCreated | Reason text and network banner only | ✓ | ✓ |
| IncidentReported, UnscheduledStopDetected, ProviderHealthChanged, CrewMessageAcknowledged | – | ✓ | – |
| CrewMessageSent | – | ✓ | ✓ (addressed to them) |

Staff fields (source, override flags, staff IDs) are removed from public events.

---

## 5. Key response shapes

```jsonc
// JourneySummary — search results, boards, saved trips
{
  "journeyId": "…", "serviceNumber": "1005", "name": "Kandy Express", "trainType": "Express",
  "serviceDate": "2026-10-14", "status": { "kind": "Late", "delayMinutes": 6 },
  "from": { "stationId": "…", "name": "Colombo Fort", "scheduledDeparture": "…Z", "expectedDeparture": "…Z", "platform": "3", "previousPlatform": "1" },
  "to":   { "stationId": "…", "name": "Kandy", "scheduledArrival": "…Z", "expectedArrival": "…Z" },
  "freshness": { "tracking": "Live", "updatedAt": "…Z" },
  "version": 12          // control version (R-18)
}
```

```jsonc
// JourneyStop — inside journey detail
{
  "sequence": 7, "stationId": "…", "name": "Rambukkana", "callType": "Stop",
  "status": "Departed", "isAdded": false,
  "scheduledArrival": "…Z", "expectedArrival": "…Z", "actualArrival": "…Z",
  "scheduledDeparture": "…Z", "expectedDeparture": "…Z", "actualDeparture": "…Z",
  "plannedPlatform": "1", "platform": "2", "previousPlatform": "1",
  "source": "Gps"            // ops / crew responses only
}
```

```jsonc
// Status kinds (formatted by apps with shared fixtures)
"OnTime" | "Late" | "Early" | "Cancelled" | "TerminatedEarly" | "Held" | "NotStarted" | "Completed"
```

`Held` is a display status computed from the hold flag (D-4); the journey's own state machine is unchanged.

---

## 6. Errors

Every error is Problem Details with a stable `code`:

```json
{ "type": "https://smartrail.example/errors/version-conflict", "title": "Journey was changed by someone else",
  "status": 409, "code": "VERSION_CONFLICT", "detail": "Changed by R. Perera 4 s ago",
  "currentVersion": 13, "requestId": "…" }
```

| HTTP | `code` | When |
|---|---|---|
| 400 | `VALIDATION_FAILED` | Field errors in `errors: {field: [message]}` (specific, fixable messages, US-P5) |
| 401 | `UNAUTHENTICATED` | Missing / expired token; **sign-in failures always return one generic message** |
| 403 | `FORBIDDEN` / `OUT_OF_SCOPE` / `CAB_SESSION_REQUIRED` / `DEVICE_NOT_REGISTERED` | Wrong role · outside own station / assigned journeys · driver action from phone · unknown device |
| 404 | `NOT_FOUND` | Unknown ID (also used instead of 403 where existence itself is sensitive) |
| 409 | `VERSION_CONFLICT` · `INVALID_STATE` | Stale version · action not allowed in current state (e.g. cancel a completed journey) |
| 422 | `RULE_VIOLATION` · `HOLD_REQUIRED` · `CONFLICT_NOT_ACKNOWLEDGED` | Business rule refused (platform not at station, terminate at passed station …) |
| 429 | `RATE_LIMITED` · `SIGN_IN_LOCKED` | Too many requests · 5 failed sign-ins |
| 503 | `PROVIDER_UNAVAILABLE` | Only on provider-specific calls; never on train information |

Error messages shown to users come from the apps; `detail` is for logs and developers, and never contains personal data except the acting staff member's display name in `VERSION_CONFLICT` (US-O4.6).

---

## 7. Story coverage (Musts)

| Story | Endpoints |
|---|---|
| US-P1–P4 | 3.1 search (incl. next trains), journey, stream |
| US-P5–P7 | 3.2 |
| US-P8 | `stations/{id}/catering` |
| US-O1 | staff sign-in, `staff/me`, scope checks |
| US-O2, O-3, O-8 | `ops/dashboard`, `ops/journeys/{id}`, `ops/alerts`, `ops/stream` |
| US-O4, O-5 | `ops/journeys/{id}/…` actions, previews |
| US-O6, O-7 | `ops/crew-reports`, `ops/incidents`, `ops/journeys/{id}/messages` |
| US-O9, O-10 | announcements (manage), `ops/audit` |
| US-C1–C7 | 3.4 |
| US-D1–D4 | 3.1 boards, platform, timetable, announcements; 3.5 |
| US-B1–B2 | seed + generator jobs (no endpoint) |
| US-B3–B5 | positions, detection (internal), streams |
| US-B6 | internal (propagation rules); visible through streams, push, webhooks |
| US-B7 | section 2 + permission matrix tests |
| US-F1–F4 | 3.6, `ops/dashboard` health |

---

## 8. Mismatches found while writing this (stories vs agreed domain model) – all resolved 12 Oct, applied to the domain model

| # | Mismatch | Proposal |
|---|---|---|
| M-1 ✔ | **Lead time:** US-O5.2 says *expected arrival − time of change*; domain model R-11 says *expected departure − now* | Use **expected arrival** (passengers start moving to the platform before the train arrives; at a terminus there is no prior arrival, so use departure there). Update R-11 |
| M-2 ✔ | **Implausible speed:** US-B3.2 says > 120 km/h; domain model R-08 says > 160 km/h | Use **120 km/h** (no train on the seeded lines is faster; a tighter limit catches GPS jumps). Update R-08 |
| M-3 ✔ | **Passenger name:** US-P5.1 asks for a name; `PassengerAccount` in the domain model has no name | Add `name` to PassengerAccount (used only for greeting; never sent to the provider) |
| M-4 ✔ | **Hold:** US-O4.1 says "hold (minutes)"; the domain model's Hold has no duration | Add optional `minutes` to Hold; R-22 still moves expected departure with the clock until released |

---

## 9. Decisions (C-1–C-7 agreed 12 Oct)

| # | Question | Proposal | Why |
|---|---|---|---|
| C-1 ✔ | Times in the API | UTC (`Z`); apps convert to Asia/Colombo | One unambiguous format; no daylight saving in Sri Lanka, but the rule stays correct if data ever crosses zones |
| C-2 ✔ | How the journey version is sent | `expectedVersion` in the request body | Visible in the OpenAPI schema and in tests; simpler for generated clients than `If-Match` headers |
| C-3 ✔ | Ops web tokens | Access token in memory + refresh token in an `HttpOnly` cookie | JavaScript can't read the cookie, so a script injected into the page can't steal a long-lived token |
| C-4 ✔ | Who writes "3 min late" | Apps, from structured status + shared fixtures | Keeps the API language-neutral (multilingual later) while the fixtures guarantee identical text |
| C-5 ✔ | Platform change in two steps (preview, then confirm) | Yes | The warning, lead-time level and conflicts appear **before** the controller commits (US-O5.5, .7) |
| C-6 ✔ | Stream in web apps | Fetch-based SSE client with the `Authorization` header | Built-in `EventSource` can't send headers; tokens in URLs leak into logs |
| C-7 ✔ | M-1 to M-4 above | As proposed | — |
| C-8 ✔ | How clients get API types | **Agreed 12 Oct:** TypeScript types generated (`openapi-typescript` + `openapi-fetch`); Dart via a generator only if its output is clean, otherwise hand-written models + contract tests; SSE event examples in `shared/events/` parsed by every app's tests. Written into the agent instructions (`CLAUDE.md`) | With agentic development, a decision only exists for the agent if it is written where the agent reads it |

---

## AI usage log

| Date | AI proposal | My decision | Why |
|---|---|---|---|
| 9 Oct | Separate endpoint `GET /journeys/{id}/next` for "next trains" after a missed or cancelled train | **Rejected:** reuse search with `after` and `limit` | The app already knows boarding and destination; one endpoint means one set of rules and tests |
| 9 Oct | (scope change) | **My decision:** one line, one controller role; region filters, chief-only audit and interchange data removed | Scope management |
