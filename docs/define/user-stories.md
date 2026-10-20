# SmartRail – User Stories and Acceptance Criteria (MVP Musts)

**Status:** Draft for review, 7 Oct 2026. Covers every **Must** in `define/moscow.md` (Shoulds get stories only if time allows).
**Format:** each story has an ID matching the MoSCoW capability (US-P1 ↔ P-1), the persona it serves, and acceptance criteria (AC) written as testable statements. **★ MoT** marks criteria that test a *moment of truth* from the journey maps – these are tested first.
**Rule references:** A-xx = assumptions log, C-x = conflicts log, SM-x = success measures.

---

## Epics

Stories are grouped into epics by **user outcome**. Each epic names who it serves, the success measure it supports and when it is built (timeline 10–16 Oct).

| Epic | Outcome | Personas | Stories | Supports | Build |
|---|---|---|---|---|---|
| **EP-1 Find a train** | A passenger finds the right train and knows what to expect | Kasun, Fathima | US-P1, US-P2 (+ P-9 Should) | – | 13 Oct |
| **EP-2 Live journey** | A passenger always knows where the train is and how late it is | Kasun, Fathima | US-P3, US-P4 | SM-1, SM-2 | 13 Oct |
| **EP-3 Account and alerts** | A commuter is told about changes before leaving home | Kasun | US-P5, US-P6, US-P7 | SM-2 | 13 Oct |
| **EP-4 Catering** | Food is ready when the train really arrives | Fathima, Dilani | US-P8, US-F1–US-F4 | SM-5 | 15 Oct |
| **EP-5 Network monitoring** | The controller sees problems first and trusts the data | Ruwan | US-O2, US-O3, US-O6, US-O8 | SM-3 | 12 Oct |
| **EP-6 Disruption management** | One decision reaches everyone safely | Ruwan, Chamari | US-O4, US-O5, US-O7, US-O9, US-B6 | SM-3, SM-4 | 12 Oct |
| **EP-7 Crew journey** | The crew reports from the train without distraction | Sunil, guard | US-C1–US-C7 | SM-2 | 14 Oct |
| **EP-8 Station displays** | Everyone in the station sees the same, trustworthy information | Fathima, Chamari | US-D1–US-D4 | SM-3 | 15 Oct |
| **EP-9 Live data platform** | Positions become delays, ETAs and events within seconds | (all) | US-B2, US-B3, US-B4, US-B5 | SM-1, SM-2, SM-3 | 10–11 Oct |
| **EP-10 Network data** | A realistic network to run and demo on | (all) | US-B1 | – | 10 Oct |
| **EP-11 Security and access** | Everyone can do exactly what their role allows, and every action is traceable | (all) | US-O1, US-O10, US-B7, Q-2 | – | 10–16 Oct |
| **EP-12 Quality and operability** | The system is tested, observable and accessible | (all) | Q-1, Q-3, Q-4 | all | 16–18 Oct |

**Build order follows the epics:** EP-10 and EP-9 first (walking skeleton by 11 Oct), then EP-5/EP-6 (ops web), EP-1–EP-3 (passenger app), EP-7 (crew app), EP-8 and EP-4 (displays and catering). EP-11 is built into every day; EP-12 closes the build.

---

## 1. Passenger app

### US-P1 Search direct trains
*As Kasun or Fathima, I want to find the trains that run between two stations on a date, so that I can choose which one to take.*
1. Station fields offer type-ahead on station names; no account is needed (A-07).
2. Results include only trains that **stop** at both stations, **origin before destination**, and **run on the chosen date** (calendar incl. Poya exceptions) (S3-15).
3. Each result shows: scheduled and expected departure and arrival, train type (Express / Slow), departure platform, status.
4. Results are sorted by departure time; past departures today are hidden.
5. If no direct train exists: "No direct trains between these stations on this date" (A-08).
6. *(Moved to Won't, 9 Oct)* "Change at" guidance needs a second line; deferred with the second line.

### US-P2 Train details and line diagram
*As Fathima, I want to see where a train stops and where it is now, so that I know it is my train and when I'll arrive.*
1. Shows all stops with scheduled and expected times and platforms; passed stops are marked.
2. Line diagram shows the train marker between stations using the distance covered (A-09, S5-08).
3. Leads with the train's name and type ("Kandy Express"); the train number is shown as secondary information.
4. Shows **today's** assigned train set: traction type (diesel power set / diesel locomotive-hauled) and seat types (class, reserved / unreserved) (S2-17). If no train set is assigned yet: "Usually: power set" in muted text.
5. Traction values are modelled so that electric can be added later, but no electric trains appear in the seed data (verify against the real network).
6. Updates live without refreshing.
7. *(Should, S-9)* An illustrated train front for the traction type, with a text description for screen readers; no copyrighted photos.

### US-P3 Live tracking: waiting and on board
*As Kasun, I want to follow my train while I wait and while I travel, so that I always know when it reaches my station.*
1. **Waiting mode:** last passed station and expected arrival at the boarding station (S3-11).
2. "I'm on this train" switches to **on-board mode:** next stop and expected arrival at the destination (S3-12).
3. If the train departs the boarding station and the passenger has not tapped: prompt "Are you on board?"; **Yes** → on-board mode; **No, I missed it** → next trains to the same destination (S3-16).
4. Works without an account for any train being viewed.

### US-P4 Disruptions and data freshness ★
*As Kasun, I want to see exactly how late my train is and why, so that I never act on a false "On time".*
1. ★ MoT: lateness is shown as exact minutes against the schedule: "On time", "3 min late", "2 min early" (A-24). A train that stops and misses its scheduled time shows growing lateness within 1 minute (SM-2); no "Delay likely" labels (C-3).
2. A displayed delay changes only when the new value holds for two consecutive updates (no flicker).
3. The controller's reason is shown when given ("signal fault near Ragama").
4. **Cancelled:** "Cancelled" plus the next 3 trains to the same destination (A-78). **Terminated early:** "Terminates at Rambukkana" plus alternatives.
5. **Platform change:** "Platform 5 (was 2)". **Stop skipped / added:** shown on the stop list.
6. **Freshness:** "updated 30 s ago"; after 2 min without data "Last seen 07:38 near Kelaniya"; after 10 min "Live location unavailable – scheduled times shown" (A-26, A-11).
7. Status is never shown by colour alone (text + icon).

### US-P5 Account: sign up, sign in, delete
*As Kasun, I want an account, so that I can save trips and get alerts.*
1. Sign up asks for name, email, phone and password (min. 8 characters); invalid input shows a specific, fixable message.
2. Sign in with email + password; wrong details give one generic message; 5 failed attempts lock sign-in for 15 minutes.
3. The passenger stays signed in on the device until they sign out (A-103).
4. "Delete account" removes or anonymises personal data and saved trips (Section 20).
5. Phone verification by SMS is not part of the MVP (SMS is a Could).

### US-P6 Saved trip
*As Kasun, I want to save my daily train, so that I see today's status as soon as I open the app.*
1. A trip (train + boarding + destination station) can be saved from search results or train details; up to 5 saved trips.
2. The home screen shows today's run of each saved trip with status, platform and freshness.
3. If the saved train does not run today (calendar), the card says so.
4. Saved trips can be removed.

### US-P7 Push notifications ★
*As Kasun, I want to be told about changes to my trip without opening the app, so that I can decide before I leave home.*
1. Push requires an account and the passenger's permission; without permission everything still appears in the app.
2. ★ MoT: for today's run of a saved trip (from 2 h before departure until arrival) a push is sent for: delay change ≥ 5 min, platform change, cancellation, early termination, a skipped / added stop at the passenger's stations (A-30).
3. Content states minutes, reason and platform where known: "06:40 to Colombo Fort now 8 min late – signal fault near Ragama" (A-117).
4. The same change is never sent twice; a push that is no longer relevant (train already departed the passenger's station) is not sent (A-118).
5. Tapping a push opens the trip.

### US-P8 Catering information and hotline
*As Fathima, I want to know where I can get food on my journey and how to order it, so that I can eat on a long trip.*
1. On train details, stops between the passenger's boarding station and destination that have catering show "Food available at Polgahawela".
2. Tapping shows the provider's menu preview: items, prices, available / sold out.
3. **Call to order** opens the phone dialler with the provider's hotline; opening hours are shown; outside hours: "Hotline closed – opens 06:00".
4. A summary is shown for the call: "Mention: Train 1005 · arriving Polgahawela ~10:52 · your coach".
5. Labels "In-app ordering coming soon" and "Orders are handled by RailCantima" are shown (RailCantima is the caterer's display name; the code name of the provider service is RailBites).
6. If the provider is unreachable: "Menu unavailable"; all train information keeps working.

---

## 2. Operations web app

### US-O1 Staff sign-in, roles and scope
*As Ruwan, I want to see and act only within my responsibility, so that actions are safe and accountable.*
1. Roles: **controller** (whole line) and station manager (own station). Chief / regional controllers and regions are deferred (scope change 9 Oct; A-61, A-62 revised).
2. Every request is checked for role **and** scope; an out-of-scope action is refused with a clear message and logged.
3. Staff accounts and display device registrations come from seed data; the system administrator role is deferred.
4. Sessions expire after 30 minutes without activity.

### US-O2 Problems-first dashboard
*As Ruwan, I want problems shown before everything else, so that I act on what matters first.*
1. Alerts panel first: unscheduled stops, stale / lost GPS, crew reports, unacknowledged instructions, catering provider unreachable – newest and most severe on top (A-67).
2. Then disrupted trains sorted by lateness; then the network line view with every running train.
3. Updates live; a new alert is visible within 5 s of the event.
4. Filters by section and station (on-time trains do not appear in the alert or disrupted lists).

### US-O3 Source and freshness of data ★
*As Ruwan, I want to see where every value came from and how old it is, so that I can trust the screen.*
1. ★ MoT: each train shows its position source (GPS / driver tap / controller / schedule) and age ("GPS · 15 s ago").
2. Stale data (> 2 min) is highlighted; lost data (> 10 min) is marked "No live data".
3. A controller override is labelled as such; if GPS later contradicts it, a warning appears until the controller keeps or clears it (A-28).

### US-O4 Controller actions on a journey ★
*As Ruwan, I want to delay, hold, cancel, terminate, skip or add stops with one action, so that everyone is informed at once.*
1. Actions: delay (minutes + reason code + optional note), hold (minutes), cancel, terminate early at a station, skip a stop, **add a stop**.
2. **Add a stop:** only stations the train passes through on its route; arrival time estimated from distance + 1 min stop; later stops move accordingly; a platform must be chosen.
3. Cancel, terminate and skip require a confirmation showing what will be affected (stations, displays, catering).
4. A cancelled journey can be **reinstated**; everyone told about the cancellation is told it runs again (A-77).
5. ★ MoT: every app, display, crew device and the catering provider show the change within 5 s (SM-3).
6. If another user changed the journey meanwhile, the save is refused: "Changed by [name] a moment ago – reload" (A-66).
7. Every action is audit-logged (US-O10).

### US-O5 Platform change with lead time and hold ★
*As Ruwan and Chamari, I want late platform changes handled safely, so that the crowd does not panic.*
1. Only the controller changes platforms (C-1, A-68).
2. Lead time = expected arrival − time of change, calculated automatically and recorded (A-112).
3. **> 15 min:** normal update everywhere.
4. **5–15 min:** also high-priority station staff alert, repeated announcement, old platform display redirects.
5. ★ MoT **< 5 min:** warning before confirming; the controller can set a hold at the new platform. With a hold, displays and announcements say "The train will wait for passengers"; without a hold, the wording makes no promise (A-113).
6. The driver receives the instruction (US-C6; guard deferred 14 Oct); station staff are alerted at the same moment as passengers (A-114).
7. **Platform conflict warning:** if another train is scheduled or expected at the chosen platform within 5 minutes of this train, a warning names it ("Platform 5 is expected to be occupied by 1520 at 18:04") before the controller can confirm.

### US-O6 Crew incident reports *(deferred 12 Oct – re-plan)*
*As Ruwan, I want crew reports to reach me immediately, so that I don't wait for a phone chain.*
1. A report appears as an alert with type, journey, location and the time it **occurred** (not when it arrived).
2. The controller creates an incident from it (reason pre-filled) or dismisses it with a reason; the report stays linked (A-79).

### US-O7 Messages to crew
*As Ruwan, I want to send instructions to a train's crew and know they received them.*
1. Send to the driver of a journey (guard deferred 14 Oct): templates (e.g. "Hold at Platform 5") or short text.
2. Status per recipient: sent → delivered → acknowledged.
3. Not acknowledged after 2 minutes → highlighted on the dashboard.

### US-O8 Unscheduled stop alert *(deferred 12 Oct – re-plan)*
*As Ruwan, I want to know quickly when a train stops where it shouldn't, so that I can act before passengers ask.*
1. A train stationary for ≥ 3 min outside any station zone creates an "Unscheduled stop" alert.
2. The alert clears when the train moves again; it is never shown to passengers as a status (C-3).

### US-O9 Station announcements
*As Chamari, I want to post announcements to my station's large screen.*
1. Station manager composes: text (max 200 characters), type (general / disruption / emergency), start and end time – own station only.
2. Text is shown as plain text only (no markup) and is audit-logged with its author (Section 17).
3. Automatic announcements are generated from events (platform change, cancellation) using templates that include "Assistance is available from staff".
4. Priority on screen: emergency > disruption > general.

### US-O10 Audit log
*As the railway, we want a record of every operational action, so that incidents can be investigated.*
1. Every operational and crew action records who, role, when, what, previous and new value, reason (A-64, A-56).
2. Entries cannot be edited or deleted.
3. The controller can view the log filtered by journey, user and time.

---

## 3. Crew app (native, driver; guard deferred 14 Oct)

*The guard role is deferred (scope decision, 14 Oct, see `moscow.md` change log): guard wording below is kept and marked, not deleted, so the stories can be revived. The driver is the only crew role.*

### US-C1 Phone mode: today's journeys
*As Sunil, I want to see my assigned journeys for today on my own phone, so that I know which train to walk to.*
1. On a phone, the app runs in **phone mode**: staff sign-in; only journeys assigned to this driver are listed (guard deferred 14 Oct), in order, with train set, time, route and departure platform (seed data).
2. Phone mode collects no location and offers no journey actions for the driver. *(Deferred 14 Oct: the guard using it for Arrived / Departed, incident reports and messages, US-C4–C6.)*
3. The list is available offline once loaded.
4. Changes to later journeys (e.g. a new platform) appear on the phone; instructions for the running journey go to the cab tablet.

### US-C2 Cab mode: start and end a journey
*As Sunil, I want one tap to start and end my run on the cab tablet, so that tracking happens without extra work.*
1. On a **registered cab tablet** (bound to a train set, kiosk, train-powered), the app runs in **cab mode**; the driver signs in with their staff account (staff ID + PIN is a Should, S-10).
2. "Start journey" is allowed only for a journey assigned to this driver, from 60 min before scheduled departure; it cannot be started from a phone.
3. One active cab session per driver: signing in on another tablet ends the previous session.
4. Starting begins GPS tracking every 10 s (positions batched); ending stops it. Location belongs to the train during the journey and is not collected outside it (A-29 revised).
5. The cab tablet is the tracking device. *(Deferred 14 Oct: the guard's phone as backup if the tablet fails, A-60.)*
6. At the final stop the app prompts "End journey".
7. *(Should, S-11)* If the journey's assigned train set differs from the tablet's train set, the driver is warned and the controller alerted.

### US-C3 Glance view ★
*As Sunil, I want only what I need while driving, so that I keep my eyes on the track.*
1. ★ MoT: shows next stop, scheduled vs expected time and delay in large text; dark theme; landscape.
2. For the **driver**, input controls are disabled while the train is moving (speed > 5 km/h). *(Deferred 14 Oct: the **guard** can use them while moving, A-51.)*
3. Touch targets are at least 64 px.

### US-C4 Arrived / Departed
*As the driver (guard deferred 14 Oct), I want to record arrival and departure when GPS fails.*
1. Large Arrived / Departed buttons for the current stop.
2. The recorded time takes priority over GPS for that stop (source priority: controller > crew > GPS > schedule, A-28).

### US-C5 Incident report *(deferred 12 Oct – re-plan)*
*As the guard (role deferred 14 Oct), I want to report a problem in a few taps.*
1. Types: obstruction on line, animal on track, landslide / flooding, signal fault, technical fault, passenger incident, other.
2. Location and time are filled in automatically; the guard can add a note (max 200 characters); the driver cannot type while moving.
3. Reaches the controller within 5 s when online (US-O6).

### US-C6 Controller messages and instructions
*As Sunil, I want instructions to reach me clearly and to confirm them simply.*
1. New messages arrive with sound and vibration.
2. The driver acknowledges with one tap when stopped. *(Deferred 14 Oct: the guard can acknowledge at any time.)*
3. A hold instruction shows platform and duration ("Hold at Platform 5 for 3 min for passengers").
4. Safety-critical instructions continue to be given by radio (A-50).

### US-C7 Offline queue
*As Sunil, I want the app to keep working in tunnels and dead zones.*
1. Actions taken offline are saved with the time they happened and sent in order when the connection returns.
2. The server uses the time they happened; an older action never overwrites newer data; duplicates are ignored.
3. The screen shows "Offline · 3 actions waiting" until they are sent.

---

## 4. Station displays

### US-D1 Main board
*As Fathima, I want to find my departure at a glance.*
1. Next 10 departures: time, destination, train type, platform, status.
2. Readable at 10 m (minimum text size defined in the design system); high contrast.
3. Departed trains disappear 1 min after departure; cancelled trains stay until their scheduled time has passed (A-35).

### US-D2 Platform display
*As Fathima, I want to confirm that the next train on this platform is mine.*
1. Next train with destination, type, expected time and the remaining stops **after this station** (scrolling if long); the following 2 trains below.
2. A non-stopping train shows "Next train does not stop here – stand back from the platform edge" (A-36).
3. After a platform change away from this platform: "Train 1520 to Kandy now departs from Platform 5".

### US-D3 Large information screen
*As Chamari, I want one screen that tells the whole hall what is arriving and what has changed.*
1. Arriving trains with origin, expected time and platform.
2. Announcements area following the priority rule (US-O9).
3. Automatic announcements for platform changes and cancellations; "The train will wait for passengers" only when a hold is set (US-O5).

### US-D4 Offline behaviour
*As Fathima, I want the board to tell me when it can't be trusted.*
1. Up to 2 min without connection: last data kept.
2. After 2 min: banner "Live information unavailable – check announcements or ask staff"; scheduled times from the cached day timetable, labelled "scheduled".
3. Trains are removed by the clock as their scheduled departures pass.
4. On reconnect the full current state is reloaded.
5. Displays use read-only credentials and cannot change any data.

---

## 5. Backend and shared

### US-B1 Seed network
1. **One line (Main Line, both directions)**, ~10–15 real Sri Lankan stations (names and order verified against an authoritative source), platforms, distances, station coordinates and 300 m zones.
2. Services with train types; train sets with **traction type** and coach classes (reserved / unreserved); a weekday / weekend calendar and one Poya-day exception.
3. *(Removed 9 Oct)* Interchange stations are not needed with one line; deferred with the second line.
4. Cab devices (registered Android tablets), each bound to a train set; crew sessions (driver + device + start / end) are created at runtime (ADR-05).
5. Staff accounts for every role and crew assignments for the demo days; operator, branding and timetable are fictional (A-20).

### US-B2 Daily journeys
1. Operational journeys are generated for the next 3 days from services + calendar (A-13).
2. Timetable changes never alter journeys already running (A-17).

### US-B3 Position intake and simulator
1. Positions are accepted only from a **registered cab device** with an active crew session for a started, assigned journey (Section 17).
4. Position times use GPS time or a server-calculated clock offset, so a wrong tablet clock cannot make a train look early or late.
2. Implausible positions are rejected: speed > 120 km/h, > 500 m off the route, moving backwards.
3. The simulator uses the **same API** as the crew app and can run scenarios at faster speed, including a stop between stations, GPS loss and a tunnel (A-21).

### US-B4 Detection, delay and ETA
1. Arrived / departed / passed detected with 300 m station zones (A-23).
2. Delay = actual − scheduled; ETA of remaining stops = scheduled + current delay; predicted departures never earlier than scheduled (A-25).
3. Stale after 2 min, lost after 10 min (A-26).

### US-B5 Event stream
1. Event types include: position updated, arrived, departed, passed, delay changed, platform changed, journey cancelled / reinstated / terminated, stop skipped / added, hold set, announcement, incident.
2. Clients receive events pushed; on reconnect they receive a full snapshot (A-102).
3. End-to-end from action to client display < 5 s (SM-3).

### US-B6 Disruption propagation
1. Each disruption type reaches the channels in the propagation matrix (Section 10), with messages from templates.

### US-B7 Authentication and authorisation
1. Separate credentials for passengers, staff, display devices and the catering provider.
2. Role and scope checked on every API request; automated tests cover the permission matrix.

---

## 6. Catering integration (light)

### US-F1 Provider service
*As Dilani, I want my own system to stay mine while working with SmartRail.*
1. A separate service with its own data store.
2. Exposes: catering stations, menus with availability, hotline number and opening hours.
3. Has an event log page listing every event received from SmartRail.
4. Receives the list of journeys calling at its stations for the day (Dilani's journey gap).

### US-F2 Events to the provider ★
*As Dilani, I want to know when trains will really arrive at my stations.*
1. ★ MoT: SmartRail sends ETA changes (≥ 1 min), cancellations, early terminations and skipped / added stops — only for catering stations.
2. Every event is signed; the provider rejects unsigned or altered events.
3. Failed deliveries are retried with increasing intervals; each event has an ID so duplicates are ignored.
4. 100% of events delivered; < 10 s when the provider is up (SM-5).
5. No passenger data is sent.

### US-F3 Menus and hotline in SmartRail
1. SmartRail fetches stations, menus and hotline details from the provider every 5 minutes and caches them.
2. If the provider is down, train information is unaffected and the app shows "Menu unavailable" (US-P8).

### US-F4 Provider health
1. Ops dashboard shows "Catering provider: connected · last event 20 s ago" or "unreachable".
2. Undelivered events for more than 5 minutes create an alert.

---

## 7. Quality, security, operations (technical stories)

| ID | Acceptance criteria |
|---|---|
| Q-1 | The 5 E2E flows in `moscow.md` run automatically and pass before code freeze (18 Oct). |
| Q-2 | Threat model written (8–9 Oct); authentication, input validation, rate limits and read-only display credentials implemented and tested. |
| Q-3 | Structured logs with request IDs and no personal data; health checks for every service. |
| Q-4 | Automated accessibility checks on passenger app and displays; manual check that no status relies on colour alone; WCAG 2.2 AA contrast. |

---

## Traceability: moments of truth → acceptance criteria

| Persona | Moment of truth | Tested by |
|---|---|---|
| Kasun | Alert arrives before leaving home | US-P7 AC2 |
| Kasun / Fathima / Chamari | Calm announcement at late platform change | US-O5 AC5, US-D3 AC3 |
| Fathima | Confirm the right train herself | US-D2 AC1 |
| Sunil | App stays out of the way while driving | US-C3 AC1–2 |
| Sunil | Report reaches controller without phone chain | US-C5 AC3, US-O6 |
| Ruwan | Trusts the screen | US-O3 AC1 |
| Ruwan | One action reaches everyone | US-O4 AC5 |
| Dilani | ETA arrives in time to change cooking | US-F2 AC1 |
| Kasun | No false "On time" | US-P4 AC1 |
