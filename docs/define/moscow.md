# SmartRail – MVP Scope (MoSCoW)

**Status:** Confirmed 6 Oct 2026; scope reduced 9 Oct (one line, one controller role – see change log). Deadline: demo and submission on **20 Oct 2026**. Build window 10–16 Oct.
**App priority:** 1. Passenger app · 2. Operations web · 3. Crew app · 4. Station displays.
**Rule:** if something slips, cut from the bottom of the Should list upwards. Never cut Musts in section 7 (quality) or the catering integration (required by brief section 23).

---

## Success measures

| # | Goal | Measure | MVP target |
|---|---|---|---|
| SM-1 | Passengers can trust live times | ETA accuracy (predicted vs actual arrival) | 80% within ±2 min |
| SM-2 | No false "On time" | Time from a train falling behind to the app showing it | < 1 min |
| SM-3 | Disruptions reach everyone at once | Controller action → every app and display updated | < 5 s |
| SM-4 | Safe platform changes | Share of platform changes with < 5 min notice | Measured and reported |
| SM-5 | Catering kept in step with real journeys | ETA / change events for catering stations delivered to the provider | 100% (with retries); < 10 s when provider is up |

---

## MUST

### 1. Passenger app (native) – priority 1
| ID | Capability |
|---|---|
| P-1 | Search direct trains between two stations: scheduled and expected times, train type, platform |
| P-2 | Train details: stopping stations, line diagram with live train position; **today's train set: traction type (diesel power set / diesel locomotive-hauled) and seat types (class, reserved / unreserved)**; train number shown as secondary |
| P-3 | Live tracking: waiting and on-board modes, "I'm on this train" tap, "Are you on board?" prompt |
| P-4 | Disruptions: delay in exact minutes with reason, cancellation with next trains, platform change "(was 1)", data freshness |
| P-5 | Sign up (email + phone), sign in (email + password), stay signed in |
| P-6 | Saved trip |
| P-7 | Push notifications for saved trips: delay change ≥ 5 min, platform change, cancellation, stop change |
| P-8 | Catering: "Food available at [station]", menu preview, **one-tap provider hotline** with opening hours and a "when you call, mention: train, arrival time, coach" summary, "In-app ordering coming soon" |

### 2. Operations web app – priority 2
| ID | Capability |
|---|---|
| O-1 | Sign-in with roles and scope (**controller** for the whole line, station manager for own station); staff accounts, scheduler data and device registrations come from seed data (no system administrator role in the MVP) |
| O-2 | Problems-first dashboard: alerts → disrupted trains → live network line view; **filters by section and station** |
| O-3 | Every value shows source and freshness (GPS / driver tap / controller, "updated 20 s ago") |
| O-4 | Controller actions: delay with reason, hold, cancel, terminate early, **skip a stop, add a stop**; confirmation for high-impact actions |
| O-5 | Platform change by the controller: automatic lead time, three levels, hold + calm announcement under 5 min; **platform conflict warning** if another train is expected at that platform |
| O-6 | Crew incident reports as alerts → create incident or dismiss **→ deferred 12 Oct (re-plan)** |
| O-7 | Messages to crew with acknowledgement shown |
| O-8 | "Unscheduled stop" alert after 3 min stopped between stations **→ deferred 12 Oct (re-plan)** |
| O-9 | Station manager posts announcements to the large information screen |
| O-10 | Audit log of every operational action (who, when, why) |

### 3. Crew app (native, driver; guard deferred 14 Oct) – priority 3
| ID | Capability |
|---|---|
| C-1 | **Phone mode** (driver's own phone): staff sign-in, read-only list of today's assigned journeys with train set, time, route and departure platform (seed data) |
| C-2 | **Cab mode** (registered Android tablet in the cab, kiosk, train-powered): driver signs in, starts journey (GPS on) / ends journey (GPS off); assigned journey only; one active cab session per driver; the driver cannot start a journey from a phone (ADR-05) |
| C-3 | Glance view: next stop, scheduled vs actual, delay; dark, landscape, large targets |
| C-4 | Arrived / Departed buttons as fallback |
| C-5 | Incident report from fixed types (free-text note was for the guard, deferred 14 Oct) **→ deferred 12 Oct (re-plan)** |
| C-6 | Controller messages and instructions (incl. "hold at Platform 5") with acknowledgement |
| C-7 | Offline queue: actions saved with timestamps, sent on reconnect |

Built with Flutter like the passenger app (ADR-04), sharing sign-in, API client, live updates and UI components. One app, two modes chosen by the device (ADR-05); the demo uses an emulator in tablet landscape as the cab device. The guard role is deferred (14 Oct): the driver is the only crew role.

### 4. Station displays (one web app, several screens) – priority 4
| ID | Capability |
|---|---|
| D-1 | Main board: next departures, train type, platform, status |
| D-2 | Platform display: next train, type, remaining stops, "does not stop here", redirect after platform change |
| D-3 | Large information screen: arriving trains + platforms, announcements (manual and automatic), sudden changes |
| D-4 | Offline: cached timetable, "live information unavailable" banner, full state reload on reconnect |

### 5. Backend and shared
| ID | Capability |
|---|---|
| B-1 | Seed network: **one line (Main Line, both directions)**, ~10–15 real Sri Lankan stations, train types, train compositions, a Poya-day calendar exception |
| B-2 | Plan / operation model, daily journey generation |
| B-3 | GPS position intake from **registered cab devices** in a started journey + simulator using the same API; device time corrected by GPS time / server clock offset |
| B-4 | Arrive / pass / depart detection; delay and ETA calculation; stale and lost data rules |
| B-5 | One event stream pushed to all apps |
| B-6 | Disruption effects through every channel (propagation matrix) |
| B-7 | Authentication for passengers, staff and devices; role + scope checks on every request |

### 6. Catering integration (light)
| ID | Capability |
|---|---|
| F-1 | Separate provider service with its own data and an event log page |
| F-2 | SmartRail sends live ETA and change events (cancelled, terminated, stop skipped / added) for catering stations: signed, retried, de-duplicated |
| F-3 | Provider supplies catering stations, menus, availability, **hotline number and opening hours**; SmartRail caches them ("Menu unavailable" if provider down) |
| F-4 | Provider connection health shown on the ops dashboard |

No passenger data is shared with the provider in this release.

### 7. Quality, security, operations
| ID | Capability |
|---|---|
| Q-1 | 5 automated E2E flows (below) |
| Q-2 | Threat model + controls: authentication, input validation, rate limits, read-only display credentials |
| Q-3 | Structured logs (no personal data) + health checks |
| Q-4 | Basic WCAG 2.2 AA checks; status never by colour alone |

**E2E flows**
1. Search → live delay update shown
2. Driver starts journey → train moves in passenger app and on platform display
3. Controller cancels → passenger push, display shows "Cancelled"
4. Controller changes platform under 5 min with hold → displays, app and crew update
5. Train delayed → catering provider receives the new ETA

---

## SHOULD – **all deferred 12 Oct (re-plan)**; build only if every Must is done
| ID | Capability |
|---|---|
| P-9 | Ticket prices for the searched journey, by class, only for classes the train has (fictional prices) |
| S-1 | Text-to-speech for announcements on the large screen |
| S-2 | Station manager station notices (e.g. lift out of service) visible to the controller |
| S-3 | Emergency button (passenger app) |
| S-4 | Network-wide incident banner |
| S-5 | Guest follows one journey without an account |
| S-6 | One incident applied to many journeys in one action |
| S-7 | Station closure: emergency close + request / approve |
| S-8 | Staff two-factor login |
| S-9 | Illustrated train front (one per traction type, with text description) on train details, and a small matching icon on the platform display |
| S-10 | Crew staff ID + PIN sign-in on the cab tablet |
| S-11 | Train set mismatch warning: journey's assigned train set ≠ tablet's train set → driver warned, controller alerted |

## COULD
Seat reservation prices (P-10) · season ticket prices (P-11) · station terminal · full connection planner with combined journeys and "connection at risk" alerts · platform occupancy view per station with free-platform suggestions · waiting-area display · "add cancelled train's stops to next train" shortcut · guard role and the guard's phone taking over tracking when the cab tablet fails (deferred 14 Oct) · automatic End journey at the terminus · phone notifications for changes to later journeys · shift handover note · metrics dashboard incl. ETA accuracy · SMS fallback · user / role management screen · system administrator role · scheduler screens · timetable editor.

## WON'T (next release – reasons in the deferred list)
In-app food ordering, payment and collection · **second line, regions, chief / regional controllers and "change at" guidance (P-12)** · ticket sales and seat booking · refunds · map view · multilingual boards · PA system integration · planned engineering works · customer service and reporting roles · on-train GPS unit · staff-first delay window · step-free routing and escorts · crew device management · masked phone calls.

---

## Change log

| Date | Change | Reason |
|---|---|---|
| 9 Oct | MVP reduced to **one line** (Main Line, both directions) and **one controller role** for the whole line. Regions, chief / regional controllers and interchanges deferred; **P-12 "Change at" moved from Should to Won't** (no second line to change to). Station manager role unchanged. `Line` stays in the data model so a second line is new data, not new code. | Scope management: saves seed verification, permission-matrix and filter work in a 7-day build; no Must or E2E flow lost |
| 12 Oct | **Re-plan:** no code existed on 12 Oct, leaving 6 build days (12–17 Oct) instead of 7, with QA reduced to one day. Deferred: **O-8** unscheduled stop alert; **O-6 + C-5** crew incident reports and incident workflow (the controller's delay reason covers the cause; none of the 5 E2E flows needs them); **all Shoulds**; station manager limited to announcements (O-9). Never cut: 5 E2E flows, auth with role + scope, audit log, catering (light), AI log | Deliberate scope management instead of running out of time on 16 Oct |
| 14 Oct | **Guard role deferred** (scope decision by Senuja). The roles are now Controller, StationManager and Driver. Journeys have a driver only; the guard accounts are removed from the seed data; crew phone mode is the driver only. The guard stays a discovered actor for future work (second crew member, backup tracking from the guard's phone). Affects C-1, C-5, US-C1/C3/C4/C6, A-59, A-60. | The brief only requires the driver; no E2E flow needs the guard; it removes a role from sign-in, the seed data and every permission-matrix row. |