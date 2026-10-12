# SmartRail – Brief Analysis (Discover Phase)

Section-by-section analysis of the assessment brief: what is **stated**, what is **implied**, and what is **missing**.
Each item has an ID for traceability into requirements, assumptions and the AI usage log.

**Source tags:** `Brief` = stated in the brief · `AI` = proposed by AI, reviewed by me · `Own` = my own finding or decision

**Status:** All sections (2–20) reviewed and agreed (5 Oct 2026). Brief analysis complete; next: targeted stakeholder simulations.

---

## Section 2 – Business Scenario

### Stated

| ID | Statement | Source |
|---|---|---|
| S2-01 | A national railway organisation operates passenger train services across a network of stations. | Brief |
| S2-02 | Thousands of passengers use the services every day. | Brief |
| S2-03 | Two business goals: modernise the passenger experience; improve visibility into operations. | Brief |
| S2-04 | Passengers need accurate information on services, routes, stations, upcoming trains, expected times, current train location ("where appropriate") and disruptions/changes. | Brief |
| S2-05 | Digital systems are needed for passengers, drivers/operational staff, stations, central operations and external service providers. | Brief |
| S2-06 | The solution consists of multiple applications and services, not a single app. | Brief |

### Implied

| ID | Implication | Why it matters | Source |
|---|---|---|---|
| S2-07 | "Accurate" needs a measurable definition (data freshness, ETA tolerance). | Cannot be tested or accepted without a number. | AI |
| S2-08 | "Modernise" implies existing processes and data (timetables, manual announcements). | Where does timetable data come from? Is there migration? | AI |
| S2-09 | "Visibility into operations" needs a **live data source** for train position, which is not named. | Biggest hidden dependency: live tracking, ETAs, displays, catering timing and disruptions all rely on it. | AI |
| S2-10 | "Where appropriate" means some cases where location must not be shown (bad data, empty-stock moves, security incidents, lost contact). | Needs explicit rules. | AI |
| S2-11 | "External service providers" is plural; only catering is named. | Decide: design for one provider, or a reusable integration. | AI |
| S2-12 | Thousands of daily passengers means **peaks**, worst during disruptions. | Load is highest exactly when things go wrong. | AI |
| S2-13 | Freight is outside "passenger services" but can delay passenger trains on shared track. | Out of scope as a feature; relevant as a delay cause. | AI |

### Added findings (own)

| ID | Finding | Decision | Source |
|---|---|---|---|
| S2-14 | Passengers want journey **fare** information by class (1st / 2nd / 3rd). | **Could-have, read-only** fare lookup. Fixed price per station pair (S4-10). | Own |
| S2-15 | Passengers want **season ticket** prices. | Could-have, read-only. | Own |
| S2-16 | Passengers want **seat reservation** costs on long-distance trains. | Show cost as information only. **Seat booking deferred.** | Own |
| S2-17 | Trains have coach classes, so the domain model needs **train composition**. | Accepted; modelled in Section 4 (train set, coach). | Own |
| S2-18 | Distinguish **planned vs current platform** and show changes ("Platform 3, was 1"). | Accepted. | Own, refined by AI |
| S2-19 | Split stopping-pattern changes into **skips a stop / extra stop / terminates early**. | Accepted; each affects passengers and catering differently. | Own, refined by AI |
| S2-20 | Each change type needs a rule for **who is told and how** (app, notification, display). | Accepted; feeds Sections 10 and 13. | AI |
| S2-21 | Platform displays could show **where each coach class stops** on the platform. | Candidate idea. | AI |

Note: platform information, platform changes, cancellations and stopping changes are already in the brief (Sections 3 and 10). S2-18 to S2-21 are refinements, not new discoveries.

**Scope decision (Section 25 evidence):** Fare *information* is included as a Could-have. Ticket sales and seat booking are **deferred** because they require payment processing, seat inventory, holds and refunds, and the brief already leaves payment ownership open for catering (Section 12).

### Missing (open questions)

- **Actors:** Who writes passenger messages during disruptions? Do passengers without smartphones matter (justifies displays and terminals)? Other external providers?
- **Business processes:** How are timetables created, published and changed (seasonal, special events)? How are disruptions decided and announced today?
- **Operational:** Number of stations, lines, trains? Operating hours and availability target? Live data freshness target?
- **Accessibility and inclusion:** Which languages? Which accessibility standard (e.g. WCAG 2.2 AA)?
- **Success measures:** How will the organisation know SmartRail worked? (Ask the product owner simulation.)
- **Fares:** ~~Distance-based, zonal or fixed per route?~~ Resolved: fixed per station pair (S4-10). Concessions? Who maintains fare tables?
- **Ambiguities:** ~~"Service", "route", "journey", "trip" used loosely~~ Resolved by the Section 4 glossary. "Where appropriate" for location: decided by whom, by what rule?
- **Conflict:** "Accurate information" vs location "where appropriate": accuracy depends on a data source the brief does not guarantee.

---

## Section 3 – Passenger Experience

### Stated

| ID | Statement | Source |
|---|---|---|
| S3-01 | Passengers get a **mobile app** to understand the network and their journeys. | Brief |
| S3-02 | Nine passenger questions: which train, next train, on time?, where is it now, which stops, when will it reach my station, disruptions, platform/time changes, connections. | Brief |
| S3-03 | The app must suit everyday commuters and occasional travellers. | Brief |
| S3-04 | The candidate decides which information and features are valuable. | Brief |

### Journey stages (structure for user journeys)

| Stage | Passenger needs |
|---|---|
| **Planning** | Which trains run between my two stations? Scheduled times at origin and destination. Fares. Connections. |
| **Before leaving** | Next train, on time?, disruptions, expected times updated automatically. |
| **Waiting at the station** | Platform and platform changes. **Where is my train now and when will it reach my station** (live tracking, waiting mode). |
| **On board** | **Where are we now, next stop, when will we reach my destination** (live tracking, on-board mode, like a ride-hailing trip). |
| **Changing trains** | Connections at the interchange station. |

### Implied

| ID | Implication | Source |
|---|---|---|
| S3-05 | "Which train should I take" requires a journey search between two stations. | AI |
| S3-06 | "My train / my station" means the app remembers a chosen trip (saved or tracked passenger trip). | AI |
| S3-07 | Proactive change alerts mean push notifications with opt-in and preferences (Section 13). | AI |
| S3-08 | Commuters want speed and saved routes; occasional travellers want guidance. | AI |
| S3-09 | Used on the move: one hand, crowds, bright sun, weak signal (Sections 15–16). | AI |
| S3-10 | Live position depends on the undefined live data source (S2-09). | AI |

### Decisions and refinements (agreed 2 Oct)

| ID | Decision | Source |
|---|---|---|
| S3-11 | Live tracking has **two modes**: *waiting* (ETA to boarding station) and *on board* (current position, next stop, ETA to destination). | Own |
| S3-12 | The passenger **taps "I'm on this train"** to switch to on-board mode. Automatic switching rejected: a passenger who missed the train would be shown as riding it. | Own |
| S3-13 | Train results show **scheduled and expected times at origin and destination**, updated automatically when delays occur. | Own |
| S3-14 | ETAs are recalculated for every remaining stop, not just the next one. | AI, derived from S3-13 |
| S3-15 | Journey search lists trains that stop at both stations, **origin before destination**, and that **run on the travel date**. | Own; order and date rules from AI |
| S3-16 | If the train departs and the passenger has not tapped, prompt "Are you on board?". "No" shows the next trains to the same destination. | AI, refining S3-12 |

### Missing (open questions)

- **Actors:** Passengers with disabilities (step-free routes, lift outages, assistance); tourists and non-native speakers. *(Removed: "someone tracking another person", out of scope. Customer service moved to Section 9.)*
- **Business processes:** Account lifecycle including account deletion; saved journeys; notification preferences.
- **Failure scenarios:** Phone offline (show data age); train location unknown ("location unavailable", never a wrong position); train cancelled after the passenger leaves home (suggest alternatives); missed connection; no direct train found; **app and platform display disagree** (single source of truth required).
- **States:** Service as seen by passenger: scheduled, on time, delayed, departed, platform changed, partly cancelled, cancelled, terminated early, arrived. Data: live, stale, scheduled only.
- **Accessibility:** Screen reader, text scaling, status not conveyed by colour alone, language support.
- **Resolved ambiguity:** "Connections from another station" means **changing trains at a station**.
- **Conflict:** Fast for commuters vs guided for occasional travellers. Resolution: personalised home with saved journeys + search for everyone.

---

## Section 4 – Railway Network

### Stated

| ID | Statement | Source |
|---|---|---|
| S4-01 | The network includes stations, train services, routes, schedules, stops, platforms, train vehicles, drivers/operators, service calendars and operational journeys. | Brief |
| S4-02 | A train passes through multiple stations on one journey. | Brief |
| S4-03 | Services vary by time, day, route and stopping pattern. | Brief |
| S4-04 | The candidate designs the domain model. | Brief |

### Key insight 1 – Separate the plan from what actually happens

| Layer | Describes | Example | Changes |
|---|---|---|---|
| **Plan** (timetable) | What *should* happen, repeating | Train 1005, Colombo Fort → Kandy, weekdays, departs 08:15 | Rarely (timetable revisions) |
| **Operation** (daily runs) | What *is* happening today | 1005 on 2 Oct is 7 min late, at Rambukkana, platform 2 instead of 1 | Constantly |

- A delay or cancellation changes **today's run**, never the timetable.
- Scheduled vs expected times (S3-13) come naturally: scheduled from the plan, expected from the operation.
- Fallback to scheduled times (A-06) = "show the plan when there is no operational data".
- Mirrors the static vs real-time split used by public transport data standards.

### Key insight 2 – "Train" and "journey" are overloaded words

- "Train 1005" = a **service** (plan) · "My train is late" = today's **operational journey** · "The train broke down" = a **train set** (vehicle).
- The brief's "operational journey" (a train's run) differs from the passenger's journey (Section 3). The glossary fixes one meaning per word, used consistently in documents, API and code.

### Glossary (agreed 3 Oct)

| Term | Meaning | Example |
|---|---|---|
| **Station** | A place where trains can stop; has platforms and facilities. | Colombo Fort |
| **Platform** | A numbered boarding area within a station. | Platform 3 |
| **Route** | An ordered path of stations in one direction, with distances. | Main Line, Colombo Fort → Kandy |
| **Service** | A scheduled, numbered train running a route on set days with a fixed stopping pattern. | Train 1005, weekdays |
| **Scheduled stop** | One station in a service's pattern: scheduled arrival/departure, planned platform, **stops or passes**. | 1005 at Rambukkana 09:40, platform 1 |
| **Service calendar** | The dates a service runs, including exceptions (public holidays). | Weekdays except holidays |
| **Operational journey** | One run of a service on one date. Delayed, cancelled, tracked; assigned a driver and train set. | 1005 on 2 Oct 2026 |
| **Journey stop status** | Live state of each stop on a run: expected/actual times, actual platform, status. | Rambukkana: expected 09:47, platform 2 |
| **Train set** | The physical train, made up of coaches. | TS-12 |
| **Coach** | One car in a train set, with class and position. | Coach C, 1st class, position 1 |
| **Driver** | Staff member assigned to an operational journey. | — |
| **Passenger trip** | A passenger's own trip from origin to destination on an operational journey. | Fort → Kandy on 1005, 2 Oct |
| **Fare** | Fixed price for a station pair and class. | Fort → Kandy, 2nd class |

### First domain model sketch

```
Station 1──* Platform
Route  1──* RouteStation (sequence, distance from start) *──1 Station
Route  1──* Service ──1 ServiceCalendar
Service 1──* ScheduledStop (arr, dep, planned platform, stops|passes)

Service 1──* OperationalJourney (date, status)
OperationalJourney *──1 TrainSet 1──* Coach (class, position)
OperationalJourney *──1 Driver
OperationalJourney 1──* JourneyStopStatus (expected/actual times, actual platform, status)

PassengerTrip ──> OperationalJourney (origin stop, destination stop, on-board flag)
Fare (origin station, destination station, class, price)      ← Could-have, read-only
```

### Implied

| ID | Implication | Why it matters | Source |
|---|---|---|---|
| S4-05 | Each day's operational journeys must be **generated** from services + calendar. | "1005 on 3 Oct" must exist before it can be tracked. | AI |
| S4-06 | "Passes" vs "stops" must be recorded. | Section 5 asks which station the train **most recently passed**. | AI |
| S4-07 | Distances between stations are needed. | Estimating position between stations on the line diagram (A-09). | AI |
| S4-08 | Coach classes and positions belong to the train set, not the service. | A different train set changes what platform displays show (S2-21). | AI |

### Decisions (agreed 3 Oct)

| ID | Decision | Source |
|---|---|---|
| S4-09 | Glossary above adopted for documents, API and code. | Own |
| S4-10 | Fares are a **fixed price per station pair and class** (not distance-based). | Own |
| S4-11 | Seed data uses **real Sri Lankan station names and line order**; the operator, branding and timetable remain fictional. | Own; fictional-operator rule from AI |

Consequences of S4-10: the fare table grows with station pairs (about 190 pairs × 3 classes for 20 stations). Acceptable for seed data; maintenance of the table becomes an operations-platform responsibility. Distances (A-15) are still kept for position estimation.

Consequences of S4-11: station names, line order and approximate distances must be **verified against an authoritative source**, not taken from AI output, which may invent stations or get the order wrong. Real names also make multilingual station names (Sinhala, Tamil, English) a realistic future need (A-05).

### Missing (open questions)

- **States:**
  - Operational journey: scheduled → assigned → running → completed; or cancelled, terminated early, held.
  - Journey stop: scheduled → approaching → arrived → departed; or passed, skipped (cancelled stop), added (extra stop).
  - Station: open / temporarily closed. Platform: in service / out of service. Train set: in service / maintenance.
- **Business processes:** Who maintains network data and timetables (scheduling role, Section 9)? Who assigns drivers and train sets? How do timetable changes take effect?
- **Details:** Interchange stations and minimum change time (connections); station facilities (step-free access, lifts, catering availability); direction of travel; overnight services crossing midnight.
- **Edge cases (deferred):** Train splitting/joining; driver change mid-journey; train set swapped mid-day.

---

## Section 5 – Live Train Information

### Stated

| ID | Statement | Source |
|---|---|---|
| S5-01 | Passengers should understand the progress of active journeys "where appropriate". | Brief |
| S5-02 | The system may need: current location, station most recently passed, next station, early / on time / late, estimated arrival, cancelled or interrupted. | Brief |
| S5-03 | The candidate designs how live information is **obtained, calculated, communicated and updated**. | Brief |

### Key insight – the four verbs are a pipeline

| Step | Question | Design |
|---|---|---|
| **Obtain** | Where does data come from? | Driver app GPS (MVP, simulated), driver arrived/departed buttons, controller input |
| **Calculate** | Raw GPS → "passed Rambukkana, 3 min late, arrives Kandy 10:48" | Station zones, delay vs schedule, ETA rules |
| **Communicate** | Who needs it, how? | One event stream to all apps; push for live screens; notifications only for significant changes; events to catering |
| **Update** | How often; what if data stops? | Every 10–30 s; stale → last seen → fall back to schedule |

### Obtain

| ID | Decision | Source |
|---|---|---|
| S5-04 | MVP position source is the **driver app's GPS**; a simulator sends positions through the same API. | Own (AI options) |
| S5-05 | An **on-train GPS unit** is the future source: more accurate and removes battery drain on the driver's phone. The position API is source-agnostic so the unit can replace the driver app without other changes. | Own |
| S5-06 | The driver app has two fallback buttons, **Arrived** and **Departed**, for use when GPS fails. | Own |

### Calculate

| ID | Decision | Source |
|---|---|---|
| S5-07 | Each station has coordinates and a ~300 m zone: entering and stopping = arrived; leaving = departed; crossing without stopping = **passed**. | AI |
| S5-08 | Position between stations = fraction of the distance covered (uses A-15). | AI |
| S5-09 | Punctuality is measured **against the scheduled time**, shown as exact minutes: "On time", "3 min late", "2 min early". No fixed "on time" tolerance band. | Own (replaced AI's 5-min band) |
| S5-10 | Trains **can run early** and the system shows it. | Own |
| S5-11 | Predicted departures are never earlier than scheduled; *actual* early departures, when detected, are reported as they happened. | AI, adjusted after S5-10 |
| S5-12 | ETA for remaining stops = scheduled time + current delay (no recovery modelling in MVP). | AI |
| S5-13 | Cancellation, termination and holds are **declared by people**, never inferred from GPS. | AI |

### Source priority (agreed 3 Oct)

| Priority | Source | Use |
|---|---|---|
| 1 (highest) | **Controller** | Manual delay, hold, cancellation, correction when GPS is wrong |
| 2 | **Driver buttons** | Arrived / Departed at a stop |
| 3 | **GPS** | Continuous position, automatic arrive / pass / depart |
| 4 | **Schedule** | Fallback when no live data |

### Communicate

- One event stream (`StationPassed`, `ArrivedAtStop`, `DepartedStop`, `DelayChanged`, `PlatformChanged`, `JourneyCancelled`, …) consumed by every app, so app and displays cannot disagree.
- Live screens (tracking, displays, operations web) receive pushed updates.
- Passenger notifications only for significant changes (A-30).
- Catering provider receives ETA and stop changes (Section 11).

### Missing (open questions)

- **Failure scenarios:** GPS lost in tunnels (real on the Main Line) → hold last position and keep ETA stable; driver phone offline → buffer and resend, never overwrite newer data with older; driver forgets to start tracking or picks the wrong journey → reject positions that do not match the route; GPS jumps or goes backwards → discard; no updates → "last seen at …" after 2 min, "location unavailable" + schedule after 10 min.
- **Actors / processes:** Who can declare cancellations: controller only, or driver too? How is a controller override cleared?
- **States:** Tracking status per journey: not started, live, stale, lost, manual only.
- **Security:** Only the driver assigned to a journey may send its positions; positions must be authenticated.
- **Privacy:** Driver GPS is personal data; collect only while their assigned journey is running (Section 20).
- **Operational:** Retention of position history (punctuality reporting); alert when data stops flowing (Section 19).
- **Business rule question:** Are early *departures* permitted operationally, or a fault the controller should be told about?
- **Resolved:** "Where appropriate" (S2-10): show position only while tracking is live; show "last seen" when stale; never a guessed position.

---

## Section 6 – Station Digital Displays

### Stated

| ID | Statement | Source |
|---|---|---|
| S6-01 | Displays on platforms, entrances, waiting areas, large information screens and smaller terminal devices. | Brief |
| S6-02 | Displays may show upcoming departures and arrivals. | Brief |
| S6-03 | Different locations have different responsibilities (platform display vs main board). | Brief |
| S6-04 | Consider how displays receive updates and behave with unreliable connectivity. | Brief |

### Display types and responsibilities (agreed 3 Oct)

| Display | Passenger question | Shows | Source |
|---|---|---|---|
| **Main board** (entrance, concourse) | Which platform is my train on, and is it on time? | Next 8–10 departures: time, destination, **train type (slow / express)**, platform, status ("On time", "5 min late", "Cancelled") | AI; train type added (Own) |
| **Platform display** | Is the next train on this platform mine? | Next train: destination, **train type**, **stopping stations**, expected time, coach / class positions, "Train approaching", "Does not stop here, stand back" | AI; type and stops (Own) |
| **Large information screen** (inside station) | What is arriving, and is anything changing? | **Arriving trains and their platforms**; **announcements shown visually while they are made**; **sudden changes** passengers must know about | Own (replaces AI's arrivals board) |
| **Waiting area** | Do I have time? | **Next trains about to depart**, with times (compact main board) | Own |

### Decisions and findings

| ID | Decision | Source |
|---|---|---|
| S6-05 | Displays are read-only and glanceable: large, high-contrast text readable at 5–20 m, including on sunny platforms. | AI |
| S6-06 | Each display is registered and configured with station, display type and platform. | AI |
| S6-07 | Displays use the same event stream as the apps, so they cannot disagree with the app. | AI |
| S6-08 | **Train type** (slow, express, …) is a property of the **service** and is shown on boards *and* in the passenger app for consistency. | Own; app consistency from AI |
| S6-09 | Long stopping-station lists on the platform display show the stops **after this station**, scrolling or paging if needed. | AI, refining Own |
| S6-10 | **Announcements** are a new domain concept: type (train, disruption, general, emergency), priority, scope (station / platform), start and end time, author. | AI, derived from Own |
| S6-11 | Two kinds of messages on the large screen: **automatic** (generated from events such as platform change or cancellation) and **manual** (posted by staff). | AI, refining Own |
| S6-12 | Showing announcements visually while they are spoken helps deaf and hard-of-hearing passengers. | Own; accessibility framing from AI |
| S6-13 | Announcements are also **spoken**, for visually impaired passengers. MVP: the large information screen reads announcements aloud with text-to-speech (English). Integration with the station PA system remains future work. | Own; text-to-speech approach from AI |
| S6-14 | Manual announcements are posted by the **station manager**. Automatic announcements come from system events. | Own |

### Offline behaviour

| Situation | Behaviour |
|---|---|
| Short drop (< 2 min) | Keep last known data. |
| Longer outage | Banner "Live information unavailable, check announcements or ask staff"; show **scheduled** times from the cached day timetable, labelled as scheduled. |
| During outage | Keep removing trains by the clock as scheduled departures pass. |
| On reconnect | Reload the **full current state**, not only new events. |

Risk: an offline display showing a wrong platform sends passengers to the wrong place, hence the banner.

### Missing (open questions)

- **Resolved (3 Oct):** waiting area = next trains about to depart; manual announcements posted by the **station manager**; boards will rotate Sinhala / Tamil / English in future, **English only in MVP**; **spoken announcements required** (S6-13).
- **Spoken announcements – open details:** in a hall with several screens, which one speaks (avoid out-of-sync echo)? Browsers block audio until a user interacts, so display devices need kiosk settings that allow it.
- **Train types:** full list (slow, express, intercity, night mail?) and who maintains it.
- **Operational:** display heartbeat and offline alerts; accurate display clocks; "No more trains today" after the last train; which stations have displays.
- **Security:** displays are public devices; read-only access, no credentials that can change data.
- **States:** display device (online, stale, offline, not configured); train on board (scheduled, on time, delayed, approaching, at platform, departed, cancelled, platform changed, does not stop here).
- **Ambiguity resolved:** "smaller terminal devices" = small screens; interactive terminals are Section 7.

---

## Section 7 – Station Terminal Application

### Stated

| ID | Statement | Source |
|---|---|---|
| S7-01 | Selected stations have interactive public terminals. | Brief |
| S7-02 | Passengers explore information without installing the app. | Brief |
| S7-03 | Possible capabilities: search destinations, explore routes, upcoming trains, station information, disruptions, available passenger services. | Brief |
| S7-04 | Runs on dedicated station devices. | Brief |
| S7-05 | Candidate chooses technology and interaction model. | Brief |

### Key insight – a shared public device, not a big phone

| | Passenger app | Station terminal |
|---|---|---|
| Device | Passenger's own | Shared by strangers |
| Session | Ongoing, remembers | 1–2 minutes, then next person |
| Login | Optional | **Never** |
| Knows location | No (A-10) | **Yes** – fixed at one station |
| Input | Phone keyboard | Touch only, standing |

Rules: the terminal **knows its station** (pre-filled origin) and **forgets everything** (auto-reset).

### Interaction model (proposed)

1. Idle screen: next departures from this station + "Where are you going?".
2. Destination: popular-destination buttons, type-ahead with on-screen keyboard, tap-to-pick line diagram.
3. Results: direct trains with time, train type, platform, status (same logic as S3-15).
4. Train details: stopping stations, live position, fares (read-only).
5. "Continue on your phone" QR code (Could-have).
6. Auto-reset after ~60 s idle with a "Still there?" warning.

### Decisions (agreed 4 Oct)

| ID | Decision | Source |
|---|---|---|
| S7-06 | Uses the same public API as the passenger app. | AI |
| S7-07 | Accessible to all: large touch targets, key controls in the lower half (wheelchair reach), plain language. | AI |
| S7-08 | Locked-down kiosk: no escape to browser / OS, read-only, no personal data. | AI |
| S7-09 | **No food ordering on the terminal.** It shows that catering is available and directs passengers to order in the **mobile app**. | Own (AI proposed information-only) |
| S7-10 | "Available passenger services" = **catering information** and **assistance**. | Own |
| S7-11 | QR "continue on your phone" is a **Could-have**. | Own |
| S7-12 | Terminal priority is **Should-have**, after displays. | Own |

### Missing (open questions)

- **Assistance:** information about assistance services, or a **"Request help" button** that alerts station staff? The second adds a new workflow (who receives it, how they respond).
- **Station facilities** (toilets, waiting rooms, taxis): in or out?
- **Actors:** who installs, configures and maintains terminals (likely station manager).
- **Failure scenarios:** offline → cached timetable + banner (as displays); faulty → "Out of service" screen + heartbeat; walk-away → auto-reset.
- **States:** device online / offline / out of service; session idle → in use → warning → reset.
- **Accessibility (deferred):** large-text mode; spoken output via the same text-to-speech as announcements.
- **Security:** kiosk lockdown, read-only, no stored personal data, protection against search spamming.

---

## Section 8 – Driver / Train Operator Application

### Stated

| ID | Statement | Source |
|---|---|---|
| S8-01 | Drivers or relevant operational personnel get a dedicated app for their responsibilities during a journey. | Brief |
| S8-02 | Possible information: assigned journeys, route, scheduled stops, timing, operational notifications, journey status. | Brief |
| S8-03 | Responsibilities deliberately not fully specified. | Brief |
| S8-04 | Candidate decides what the driver needs and what they may update. | Brief |

### Key insights

- **The driver's job is driving.** Stopped: full use. Moving: glance-only (next stop, timing, delay); no typing or menus.
- **Driver reports, controller decides** (agreed 4 Oct).

| Train crew **can** | Train crew **cannot** (controller / station manager decides) |
|---|---|
| Start / end assigned journey | Change the timetable |
| Tap Arrived / Departed | Cancel or terminate a journey |
| Report a delay with a reason code | Change a platform |
| Report an incident | Skip or add a stop |
| Acknowledge controller messages | Edit an ETA directly |

### Users of the app (agreed 4 Oct)

| Role | Uses app in MVP | Responsibilities in app |
|---|---|---|
| **Driver** | Yes | Start / end journey (starts GPS tracking), glance view while moving, Arrived / Departed when stopped |
| **Guard** | Yes | Arrived / Departed, delay reasons, passenger incident reports, controller messages; can use the app while moving because the guard is not driving |
| Assistant driver | Deferred | Same view as driver |
| Travelling ticket inspector | Deferred | Ticketing out of scope |
| Station master / platform staff | Deferred (strong future data source) | Confirm actual arrival / departure at their station |
| Railway security officers | Deferred | Incident reporting |
| Catering staff on board / at stations | No – external provider's own system | — |
| Track maintenance, depot, signalling staff | Out of scope | Infrastructure, not passenger operations |

### Proposed workflow

1. Log in with a staff account (first app that requires login).
2. My journeys today (assigned only).
3. Pre-departure: confirm journey and train set.
4. **Start journey** → GPS tracking on (assigned journey only, within a window before departure).
5. In-journey view: next stop, scheduled vs actual, delay, remaining stops, controller messages.
6. At stops: Arrived / Departed fallback; delay reason.
7. **End journey** → GPS tracking off (A-29).

### Missing (open questions)

- **Two devices per journey:** if driver and guard phones both send GPS, positions conflict. Proposed: one tracking device per journey (driver's), guard's device as automatic backup when the driver's goes stale.
- **Radio:** safety-critical instructions stay on the existing radio as the primary channel; radio is also the backup when the app or network fails.
- **Failure scenarios:** no signal mid-journey (stored at start, actions queued with timestamps); journey cancelled or reassigned while running (shown prominently); phone battery dies; lost or stolen phone (remote logout).
- **States:** journey (assigned → started → in progress → completed / cancelled by controller); crew member (off duty / on duty).
- **Security:** staff authentication, automatic logout at end of shift, audit log of every crew action.
- **UX:** cab-mounted landscape device, dark theme for night driving, large touch targets.
- **Operational:** today's journeys only or upcoming too? Several journeys per day?

---

## Section 9 – Railway Operations Platform

### Stated

| ID | Statement | Source |
|---|---|---|
| S9-01 | Web-based system for managing and overseeing railway operations. | Brief |
| S9-02 | Users must not be a single "Administrator" role. | Brief |
| S9-03 | Example responsibilities: network operations, scheduling, stations, train services, customer information, incident management, reporting, system administration. | Brief |
| S9-04 | Candidate identifies roles and responsibilities. | Brief |
| S9-05 | Authorised users understand what is happening across the network now. | Brief |

### Key insights

- The brief is testing **access control**: roles (what) + **scope** (where), least privilege, and separation of duties (system administrator manages accounts but cannot run trains).
- The live view should be **exception-first**: alerts → disrupted trains → full network.

### Roles (agreed 4 Oct)

| Role | Responsibilities | Scope | MVP |
|---|---|---|---|
| **Chief controller** | Oversees all regions; can act anywhere; coordinates trains crossing regions | Whole network | Yes |
| **Regional controller** | Live monitoring, delays, holds, cancellations, terminations, skip / add stop, GPS override, crew messages, incidents, network passenger messages | **Own region** | Yes |
| **Station manager** | **Platform changes**, announcements, station displays and terminals | **Own station(s)** | Yes |
| **Scheduler** | Stations, routes, services, calendars, fares, train types; crew and train set assignment | Whole network | Yes |
| **System administrator** | Staff accounts, roles, device registration; **no operational actions** | Whole system | Yes |
| Customer information officer | Passenger disruption messages | Network | Folded into controller |
| Reporting viewer | Read-only reports | Network | Deferred |
| Customer service | Complaints and feedback | — | Deferred |
| Separate incident / service desks | — | — | Deferred (in controller for MVP) |

### Permissions

| Action | Chief ctrl | Regional ctrl | Station mgr | Scheduler | Sys admin |
|---|---|---|---|---|---|
| View live network | ✓ all | ✓ own region | ✓ own station | ✓ | – |
| Delay / hold / cancel / terminate / skip or add stop | ✓ | ✓ own region | – | – | – |
| **Change platform** | – | – | **✓ own station** | – | – |
| Post station announcement | – | – | ✓ own station | – | – |
| Post network passenger message | ✓ | ✓ own region | – | – | – |
| Edit timetable, stations, fares, train types | – | – | – | ✓ | – |
| Assign crew and train sets | – | – | – | ✓ | – |
| Manage users, roles, devices | – | – | – | – | ✓ |

### Decisions

| ID | Decision | Source |
|---|---|---|
| S9-06 | ~~Platform changes are made only by the station manager.~~ **Revised 5 Oct:** the controller assigns platforms; the station manager informs the controller. | Own, revised after simulations |
| S9-07 | Controllers are organised **by region under a chief controller**. Region is a new domain concept: a set of stations / route sections. | Own |
| S9-08 | MVP roles: chief controller, regional controller, station manager, scheduler, system administrator. | Own (AI proposal + regions) |

### Missing (open questions)

- **Cross-region trains:** which regional controller owns a journey that crosses a boundary? Proposed: the region the train is currently in, with handover at the boundary; chief controller sees and can act on all.
- **Chief vs regional conflicts:** a chief controller action overrides a regional one.
- **Platform change when no station manager is on duty** (night, unmanned station): who acts?
- **Processes:** timetable draft → review → publish from an effective date; controller shift handover.
- **Failure scenarios:** two people editing the same journey at once; operations platform down (apps keep last data, staff use radio / phone); wrong train cancelled by mistake (confirmation + undo).
- **Security:** strong staff authentication (possibly two-factor), session timeouts, reason + audit for every operational action.
- **UX:** dense, scannable, keyboard-friendly interface for long shifts.

---

## Section 10 – Service Disruptions

### Stated

| ID | Statement | Source |
|---|---|---|
| S10-01 | Operations do not always follow the schedule. | Brief |
| S10-02 | Types: delays, cancellations, platform changes, temporary station closures, route changes, unexpected termination, operational incidents. | Brief |
| S10-03 | Handle disruptions and communicate them to affected users. | Brief |
| S10-04 | Candidate decides how disruption information propagates through the apps. | Brief |

### Key insight 1 – one cause, many effects

| Concept | Example | Created by |
|---|---|---|
| **Incident** (cause) | Landslide between Rambukkana and Kadugannawa, from 14:10, expected clear ~16:00 | Controller (from own knowledge or a crew report) |
| **Effects** (journey changes) | 1005 held, 1007 terminated at Rambukkana, 1009 cancelled | Controller, in one action |

Benefits: one action instead of many; passengers see the reason; resolution knows which journeys to update; reports by cause.

### Key insight 2 – propagation matrix

| Event | Declared by | Passenger app | Push | Station displays | Large screen + spoken | Crew app | Catering | Ops dashboard |
|---|---|---|---|---|---|---|---|---|
| Delay | GPS or controller | ✓ live times | ≥ 5 min change | ✓ | If significant | ✓ | ✓ ETA | ✓ |
| Cancellation | Controller | ✓ + next trains | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Terminates early | Controller | ✓ + alternatives | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Platform change | Station manager | ✓ | ✓ | ✓ | ✓ | ✓ | – | ✓ |
| Stop skipped / added | Controller | ✓ | ✓ (that stop) | ✓ | ✓ | ✓ | ✓ if collection station | ✓ |
| Station closed | **Station manager requests → controller approves** | ✓ | ✓ | "Station closed" | ✓ | ✓ | ✓ | ✓ |
| Incident | Controller | Reason on affected trains; **network-wide incidents as an in-app alert banner** | With effects | Reason text | ✓ | ✓ | – | ✓ |

### Decisions (agreed 4 Oct)

| ID | Decision | Source |
|---|---|---|
| S10-05 | **Crew-reported incidents:** driver / guard report (type from fixed list, location from GPS, optional note) → alert on controller dashboard → controller creates an incident or dismisses with reason. Report linked to incident for audit. | Own; workflow detail from AI |
| S10-06 | **Station closure:** the station manager **requests**, the controller **approves**, then the closure takes effect. | Own |
| S10-07 | Incidents affecting the **whole network** appear as an **in-app alert banner** for all passengers (no account needed); the banner updates when resolved and then clears. | Own |
| S10-08 | **Planned engineering works are out of scope.** | Own |
| S10-09 | Passenger messages come from templates per event type, plus an optional short reason from the controller. | AI |
| S10-10 | Reinstating a mistakenly cancelled journey notifies everyone told about the cancellation. | AI |

### Passenger incident reporting (agreed 4 Oct)

| Need | Approach | MVP |
|---|---|---|
| Emergency (danger to life, obstruction ahead of a train) | **Emergency button** in passenger app and terminal showing emergency numbers with one-tap call (police 119, railway control hotline – verify real numbers) | **Yes** |
| Non-urgent issue | Report form to an **unverified** queue; controller confirms with crew / station before acting | **Deferred** (with customer service) |

Reasons not to build a simple in-app report button: too slow for emergencies; creates an expectation of response; false reports; no passenger GPS (A-10); new workflow scope.

### Missing (open questions)

- **Emergency station closure (agreed):** in an emergency (fire, evacuation) the station manager closes immediately and the controller is notified at once; normal closures need controller approval.
- **Failure scenarios:** cancelled train still moving (ignore its positions for passengers); knock-on delays from train set reuse (deferred); several messages at once on the large screen (emergency > disruption > general).
- **Route changes:** represented as skipped / added stops; full diversions to another line deferred.
- **Out of scope:** refunds and compensation (ticketing out of scope).

---

## Sections 11 & 12 – Catering Integration and Food Ordering

### Stated

| ID | Statement | Source |
|---|---|---|
| S11-01 | An external catering provider runs its own platform and manages food services. | Brief |
| S11-02 | SmartRail must integrate with it. | Brief |
| S11-03 | Provider may need services, routes, stations, scheduled and actual / estimated arrival times, service changes. | Brief |
| S11-04 | Integration coordinates food preparation and collection with journeys. | Brief |
| S12-01 | Passengers discover food for their journey and receive or collect it at an eligible point. | Brief |
| S12-02 | Workflow deliberately incomplete; 11 questions listed. | Brief |
| S12-03 | Provider stays a separate external system; architecture must show the separation. | Brief |

### Key insight 1 – data ownership

| Data | Owner | Other side |
|---|---|---|
| Menu, prices, availability, **lead time per station** | Provider | SmartRail reads and shows |
| Orders and status | Provider | SmartRail keeps a reference (order ↔ passenger trip) |
| Payment | Provider | SmartRail never handles money |
| Preparation and handover | Provider staff | SmartRail told when collected / not collected |
| Schedule, live ETAs, changes | SmartRail | Provider receives |
| Passenger account, **no-show record** | SmartRail | Provider gets minimum data only |

### Key insight 2 – two-way integration

| Direction | What | How |
|---|---|---|
| SmartRail → Provider | Stations / timetable sync, live ETA and change events, new orders with journey context | API calls + signed events |
| Provider → SmartRail | Menus, availability, lead times, order status (accepted, ready, collected, not collected, cancelled) | API calls + signed events |

Robustness: retries (no lost events), duplicate protection, message signatures, graceful failure ("Food ordering temporarily unavailable" while train information keeps working).

### Ordering flow

1. Passenger has a saved trip (account required, A-07).
2. App lists eligible collection stations between boarding and destination.
3. Passenger chooses station, **handover type** (platform counter or coach delivery) and sees that station's menu.
4. Order placed before the cut-off; provider accepts or rejects.
5. Live ETAs flow to the provider.
6. Handover with order code / QR; provider marks collected; app shows "Collected".

Order states: submitted → accepted / rejected → preparing → ready → collected; or cancelled (passenger / provider / SmartRail) or **not collected**.

### Decisions (agreed 5 Oct)

| ID | Decision | Source |
|---|---|---|
| S12-04 | **Both handover types**, chosen by the passenger: **platform counter** or **delivery to the coach**. | Own |
| S12-05 | Coach delivery needs the passenger's **coach number**; counter collection is only offered where the train stops long enough (or at the passenger's destination). | AI, derived from S12-04 |
| S12-06 | **Pay on collection** in the MVP; online payment deferred (provider-owned). | Own |
| S12-07 | Cut-off is **not fixed**: it depends on the collection station and its distance from the provider's kitchen. Cut-off = expected arrival − provider **lead time** for that station (and handover type). | Own; formula and provider ownership of lead time from AI |
| S12-08 | Cut-off follows the **live** expected arrival; if a train runs early and the cut-off has passed, the provider decides whether it can still fulfil. | AI |
| S12-09 | SmartRail automatically cancels orders when collection becomes impossible (train cancelled, stop skipped, station closed, train terminates before the station). | AI |
| S12-10 | Simulated provider: **decision deferred** (small independent app vs mock). | Own |

### Uncollected orders with pay on collection (agreed 5 Oct: A + B + C)

Problem: with no prepayment, a passenger can order and never collect; the provider loses the food.

| Option | Effect |
|---|---|
| A. Reminder before cut-off: "Still collecting at Kandy?" | Fewer no-shows before food is prepared |
| B. **No-show strikes:** after e.g. 2 genuine no-shows, ordering suspended for 30 days | Discourages abuse; SmartRail must enforce it because the provider does not know the passenger's identity (A-91) |
| C. Limit items / value per pay-on-collection order | Caps the provider's loss |
| D. Online prepayment (future) | Removes the problem; deferred |

Fairness rule: orders cancelled because of a disruption (delay, skipped stop, platform change, station closure) **never count as no-shows**.

### Proposed answers to the brief's 11 questions

| Question | Answer |
|---|---|
| Which stations support catering? | Provider decides; station "catering available" flag. |
| When can an order be placed? | Once a trip is saved, until the station-specific cut-off. |
| How close to arrival can it be modified? | Until the cut-off. |
| Train delayed? | Provider receives new ETA; cut-off moves with it; for long delays provider decides keep / cancel. |
| Missed collection station? | Order becomes "not collected"; no automatic transfer. |
| Can an order be cancelled? | Passenger before cut-off; provider any time with reason; SmartRail automatically when collection is impossible. |
| Who owns payment? | Provider (pay on collection in MVP). |
| Who owns the menu? | Provider. |
| Availability changes? | Provider pushes updates; re-checked when the order is placed. |
| Orders ↔ passengers and journeys? | Order ID ↔ passenger trip (journey + collection stop + handover type) ↔ account. |
| Collection confirmed? | Provider staff scan / enter order code → "collected" event to SmartRail. |

### Missing (open questions)

- Allergen information (provider data, shown in app).
- Item limits; group orders (deferred).
- Catering staff are users of the provider system, not SmartRail.

---

## Sections 13–16 – Notifications, Identity, UI/UX, Connectivity (agreed 5 Oct)

### Section 13 – Notifications

Brief: "Different users may need to receive information when important events occur." (No further detail – design is ours.)

| Recipient | Events | Channel |
|---|---|---|
| Passenger (tracking / saved trip) | Delay change ≥ 5 min, platform change, cancellation, early termination, stop change | Push (opt-in) + in-app |
| All passengers | Network-wide incident | In-app alert banner |
| Passenger with food order | Accepted / rejected, "still collecting?" reminder, ready, cancelled (and why), not collected | Push + in-app |
| Driver / guard | Journey cancelled or reassigned, controller messages (acknowledgement required), platform change at next stop | Crew app (push + in-app) |
| Controller | Crew incident reports, stale / lost GPS, override contradicted by GPS, closure requests, offline displays / terminals | Dashboard alerts |
| Station manager | Closure request approved / rejected, display or terminal offline at own station | Ops web alerts |
| Catering provider | ETA changes, stop / journey changes, new / cancelled orders | System events |
| Passenger (critical only) | Cancellation / early termination of a saved trip when the app or push is not working; food order confirmation and cancellation | **SMS** (fallback channel) |

Missing / decisions: push is not guaranteed (the in-app state is the source of truth); passengers who refuse push permission get in-app only; no duplicate or repeated notifications for the same change; push service (e.g. Firebase Cloud Messaging) is an external dependency.

### Section 14 – Identity and access

| Who | How they are identified | Access |
|---|---|---|
| Public passenger | Nobody (anonymous) | Timetable, live info, search, fares, emergency numbers |
| Passenger with account | Passenger account | + saved trips, alerts, food orders |
| Display / terminal | Registered device credential (read-only) | Public data for its station |
| Driver / guard | Staff account, logout at end of shift | Own assigned journeys only |
| Operations staff | Staff account + two-factor | Role + scope (network / region / station) |
| Catering provider | System credential + signed messages | Its own integration endpoints only |

**Decided (5 Oct):** sign-up asks for **email and phone number**; sign-in uses **email and password** (SMS codes can be delayed and passengers are busy). Follow-ups: email verification at sign-up, password reset by email, long-lived sign-in so passengers rarely log in again. **Phone number purposes (5 Oct):** (1) SMS for critical messages when the app is not working, (2) SMS confirmations for food orders (ticket purchases later), (3) the catering provider can call the passenger about an active order. Still to decide in Develop: existing identity product vs building login.

### Section 15 – UI/UX and design system

Proposal: **one design system, five densities.**

| Shared everywhere | Different per surface |
|---|---|
| Brand (fictional, A-20), typography, icons (train type, platform, catering) | Passenger app: touch, glanceable, one main answer per screen |
| **Status vocabulary and colours**: "On time", "5 min late", "2 min early", "Cancelled", "Platform 2 (was 1)" – same words, same colours, same icons in every app | Crew app: dark, landscape, very large targets |
| Status never shown by colour alone | Terminal: kiosk, very large targets, controls in lower half |
| Accessibility target WCAG 2.2 AA | Displays: readable at 5–20 m, high contrast, no interaction |
| | Ops web: dense, keyboard-friendly |

"Understand quickly while moving through a busy station": large numerals for times and platforms, one primary answer per screen, high contrast for sunlight, freshness shown (A-11).

### Section 16 – Connectivity and resilience

| App | When the connection drops |
|---|---|
| Passenger app | Show last data with its age (A-11); saved trips keep their schedule; nothing critical queued |
| Crew app | Journey stored at start; actions queued with timestamps and sent later (A-55) |
| Displays / terminals | Cached day timetable + "live information unavailable" banner (A-34, A-47) |
| Ops web | Clear "connection lost" banner; **actions are not queued** – acting on stale information is unsafe |
| Catering integration | Retries and duplicate protection (A-92) |
| All clients | On reconnect, reload full current state, not only missed events |

---

## Sections 17–20 – Security, QA, Observability, Data and Privacy (agreed 5 Oct)

### Section 17 – Security

Brief: security throughout the lifecycle, not only after development.

**Security decisions already made:** no passenger GPS (A-10); driver location only during a journey (A-29); only the assigned crew can send positions; read-only device credentials for displays / terminals; kiosk lockdown; staff two-factor; roles + scope + separation of duties; audit log of every operational and crew action; signed messages with the provider; SmartRail never handles payment; minimal data to the provider; ops web does not queue offline actions; confirmation for high-impact actions.

**Main threats to cover in the threat model (11 Oct):**

| Threat | Example | Control |
|---|---|---|
| Fake train positions | Someone posts positions for a train | Authenticated crew, journey assignment check, plausibility checks |
| Controller account takeover | Attacker cancels trains network-wide | Two-factor, scope limits, audit, confirmation steps |
| Malicious text on public screens | Announcement containing script or offensive text | Text-only rendering, length limits, author audit |
| Seeing other people's data | Passenger opens another passenger's order by changing an ID | Ownership check on every request |
| Forged provider messages | Fake "order collected" event | Signature verification |
| Overload at peak | Everyone opens the app during a disruption; scraping | Rate limits, caching of public data |
| Password attacks | Guessing passenger or staff passwords | Rate-limited sign-in, lockout, strong hashing |
| Vulnerable or AI-generated code | Insecure dependency or unreviewed generated code | Dependency scanning, code review of all AI output |

Lifecycle practices: threat model at design; secure coding checklist; dependency and secret scanning in the build pipeline; authorisation tests for every role; security review of AI-generated code.

### Section 18 – Quality assurance

Brief: test strategy covering the full spectrum; automate **5 critical end-to-end flows**.

**Proposed 5 end-to-end flows:**

1. Passenger searches two stations and sees scheduled and expected times update live after a delay.
2. Driver starts a journey; GPS positions move the train on the passenger tracking screen and the platform display.
3. Controller cancels a journey → passenger notified, display shows "Cancelled", food order at a later station auto-cancelled.
4. Station manager changes a platform → displays, passenger app and crew app all show the new platform.
5. Passenger orders food → provider accepts → ready → collected (pay on collection).

**Test layers:** unit tests for rules (ETA, punctuality, cut-off, permissions, no-show strikes); API / integration tests; contract tests with the catering provider; the 5 end-to-end flows; load test of a disruption peak (S2-12); accessibility checks; authorisation matrix tests; offline / reconnect tests.

**Testability decision:** the system clock must be **controllable in tests** – timetables, ETAs, cut-offs and staleness all depend on time.

### Section 19 – Observability and operations

| Area | What |
|---|---|
| Logs | Structured, with a request ID followed across services; no personal data |
| Technical metrics | API latency and errors, position ingestion delay, event delivery delay, notification failures, provider integration failures |
| Health | Health checks per service; display / terminal heartbeats (A-38) |
| Alerts | No GPS from a running journey for 5 min; event delivery backlog; provider unreachable; displays offline |
| Business metrics | Punctuality; **ETA accuracy (predicted vs actual arrival)** – gives "accurate" (S2-07) a measurable meaning |
| Operations | Backups, deployment and rollback steps, short runbooks for the main alerts |

### Section 20 – Data and privacy

| Class | Data | Who sees it | Retention (proposed) |
|---|---|---|---|
| **Public** | Network, timetable, live train positions, disruptions, fares, menus | Everyone | As long as useful |
| **Passenger personal** | Name, email, phone, password hash, saved trips, push tokens, orders, no-show record | The passenger; system only | Until account deletion; trip history 90 days |
| **Staff personal** | Staff accounts, driver / guard GPS during duty, action audit | Operations roles as needed; system administrator for accounts | Raw GPS 30 days, then aggregated; audit 1 year |
| **Operational** | Incidents, crew reports, controller notes, device registry | Operations roles by scope | 1 year |
| **Secrets** | Credentials, keys | Nobody (secret store) | Rotated |

Rules: logs contain IDs, never email, phone, passwords or tokens; the provider gets order reference, first name, train, station, coach, time and – for active orders only, with consent – the phone number (A-91); controllers see journeys, not passenger identities; account deletion removes or anonymises personal data; privacy notice and consent for push. Sri Lanka's Personal Data Protection Act (2022) – check applicability.

---


### Stakeholder simulation decisions (5 Oct)

**C-2 – Lead time for platform changes (resolved).** Lead time = time between the platform change and the train's expected arrival. A minimum lead time cannot be promised because changes are caused by sudden events, so the system calculates the lead time and responds to it:

| Lead time | Behaviour |
|---|---|
| > 15 min | Normal update everywhere; normal announcement; push to affected passengers |
| 5–15 min | Plus high-priority staff alert, repeated announcement, old platform display redirects ("This train now departs from Platform 5") |
| **< 5 min** | Controller sees a warning before confirming. **The train is held at the new platform** so passengers can cross safely: the controller sets the hold, the driver / guard are instructed (crew app + radio), and announcements and displays **calmly reassure passengers that the train will wait**; staff guide passengers across the footbridge. If the controller cannot hold the train, the wording does not promise a wait. |

- Station staff are alerted at the **same moment** as passengers (high-priority alert); a timed 60-second staff-first delay is deferred.
- If the station manager has reported the lift to the new platform out of service, the controller sees it before confirming and staff receive a mobility alert.
- Every platform change records its lead time (operations metric: late and very late changes per station).
- The hold extends the stop time, so later ETAs, displays and catering times update automatically through the event stream.

## Assumptions log

| ID | Assumption | Status |
|----|------------|--------|
| A-01 | MVP uses a small seeded network (2-3 lines, 15-20 stations) to represent the national network, using real Sri Lankan station names (S4-11). | Proposed |
| A-02 | No live signalling feed exists. Train position comes from the GPS of the crew app on the dedicated cab tablet (simulated in MVP, A-57); on-train GPS unit in future. | Confirmed |
| A-03 | Freight is out of scope; delays can be recorded with an external cause. | Proposed |
| A-04 | The network is in a single time zone (Sri Lanka, UTC+05:30). | Proposed |
| A-05 | MVP is in English, built ready for translation (Sinhala and Tamil station names a likely future need). | Proposed |
| A-06 | When live data is unavailable, all apps fall back to scheduled times and label them clearly. | Proposed |
| A-07 | Timetable, live information and journey search are public, no login. An account is needed only for saved journeys, alerts and food orders. | Confirmed |
| A-08 | MVP journey search shows direct trains only; if none, say so. Automatic one-change journeys are a Could-have. | Confirmed |
| A-09 | Train progress is shown as a stop-by-stop line diagram with a moving train marker. Map view deferred. | Confirmed |
| A-10 | The app does not use passenger GPS. Train position comes from the train / driver device. | Confirmed |
| A-11 | Every live value shows its freshness ("updated 1 min ago" / "scheduled, no live data"). | Proposed |
| A-12 | The model separates the plan (services, scheduled stops, calendar) from operations (operational journeys, stop status). | Proposed |
| A-13 | Operational journeys are generated from services + calendar for each date, a few days ahead. | Proposed |
| A-14 | One train set and one driver per operational journey for the whole run; no split/join, no driver change. | Proposed |
| A-15 | Distances between stations are stored on the route (used for position estimation). | Proposed |
| A-16 | Each direction of travel is a separate route. | Proposed |
| A-17 | Timetable changes apply from an effective date and never alter journeys already running. | Proposed |
| A-18 | Times are stored against the service date, so an overnight run stays one operational journey. | Proposed |
| A-19 | Fares are a fixed price per station pair and class, the same in both directions. | Confirmed (direction symmetry proposed) |
| A-20 | Station names and line order are real (Sri Lanka); operator, branding and timetables are fictional and must not imitate the real operator. | Confirmed |
| A-21 | Positions come from the driver app GPS; in the MVP a simulator sends them through the same API. | Confirmed |
| A-22 | The driver has Arrived and Departed buttons as a fallback when GPS is unavailable. | Confirmed |
| A-23 | Each station has coordinates and a ~300 m zone for arrive / pass / depart detection. | Proposed |
| A-24 | Punctuality is shown as exact minutes against the scheduled time (early / on time / late), no tolerance band. | Confirmed |
| A-25 | ETA = scheduled + current delay, no recovery. Predicted departures never earlier than scheduled; actual early departures are reported. | Proposed |
| A-26 | Stale after 2 min without an update; location unavailable after 10 min, then schedule fallback. | Proposed |
| A-27 | Cancellations, terminations and holds are declared by people, never inferred from GPS. | Proposed |
| A-28 | Source priority: Controller > Driver buttons > GPS > Schedule. A controller override stays until the controller clears it. | Confirmed |
| A-29 | Train location (from the cab tablet) is collected only while an assigned journey is running; no GPS on personal phones. | Proposed |
| A-30 | Passenger notifications only for significant changes: delay change ≥ 5 min, platform change, cancellation, stop change. | Proposed |
| A-31 | Displays are a web app running full-screen on station screens, configured by station, display type and platform. | Proposed |
| A-32 | MVP display types: main board, platform display, large information screen; waiting area as a compact variant of the main board. | Proposed |
| A-33 | Displays are read-only, receive the same live events as the apps, and reload full state on reconnect. | Proposed |
| A-34 | Displays cache the day's timetable; offline they show scheduled times with a "live information unavailable" banner and remove departed trains by the clock. | Proposed |
| A-35 | Departed trains disappear 1 min after departure; cancelled trains stay until their scheduled time has passed. | Proposed |
| A-36 | The platform display warns when the next train does not stop. | Proposed |
| A-37 | The station manager posts announcements that appear on large information screens with priority over normal content; system events also create automatic announcements. | Confirmed |
| A-38 | Displays send a heartbeat; offline displays are visible in the operations platform. | Proposed |
| A-39 | Each service has a train type (slow, express, …), shown on displays and in the passenger app. | Confirmed |
| A-40 | Announcements are read aloud with text-to-speech on one designated large screen per area (English in MVP). | Proposed |
| A-41 | Display boards are English-only in the MVP; Sinhala / Tamil / English rotation is future work. | Confirmed |
| A-42 | The terminal is a touch web app in kiosk mode, configured to its station (default origin). | Proposed |
| A-43 | No login, no personal data; session resets after 60 s idle, with a warning. | Proposed |
| A-44 | The terminal uses the same public API as the passenger app. | Proposed |
| A-45 | QR "continue on your phone" is a Could-have. | Confirmed |
| A-46 | No food ordering on the terminal; it shows catering availability and points to the mobile app. | Confirmed |
| A-47 | Offline behaviour matches the displays (cached timetable + banner). | Proposed |
| A-48 | Terminals send a heartbeat and show "Out of service" when faulty. | Proposed |
| A-49 | Terminal priority: Should-have, after displays. | Confirmed |
| A-50 | SmartRail is an information tool. Safety-critical instructions stay on the existing railway radio, which is also the backup channel when the app or network fails. | Confirmed (wording to verify) |
| A-51 | While moving, the driver's view is glance-only; the guard may use the app while moving. | Proposed |
| A-52 | Train crew report, the controller decides (permission table, Section 8). | Confirmed |
| A-53 | Delay reasons come from a fixed list. | Proposed |
| A-54 | Start journey on the cab tablet switches GPS on (assigned journey only, near departure time); End journey switches it off. | Proposed |
| A-55 | Journey data stored on the device at start; offline actions queued with timestamps and sent on reconnect. | Proposed |
| A-56 | Every crew action is audit-logged. | Proposed |
| A-57 | A dedicated tablet is mounted in the driver's cab (landscape, dark theme, large touch targets), registered to a train set, and runs the crew app full-screen. Decided 7 Oct. | Confirmed |
| A-58 | The crew app is a mobile app built with the same technology as the passenger app. | Proposed |
| A-59 | MVP crew app users: driver and guard. Other operational roles deferred. | Confirmed |
| A-60 | One tracking device per journey: the cab tablet. The guard's phone takes over (Could) when the tablet's data goes stale. | Proposed |
| A-61 | Operations roles: chief controller, regional controller, station manager, scheduler, system administrator (MVP). | Confirmed |
| A-62 | Station manager permissions are limited to their own station(s); regional controller permissions to their region. | Confirmed |
| A-63 | The system administrator manages accounts and devices but cannot take operational actions. | Proposed |
| A-64 | Every operational action records who, when and why in an audit log. | Proposed |
| A-65 | High-impact actions (cancel, terminate, skip a stop, publish a timetable) need a confirmation step. | Proposed |
| A-66 | Concurrent edits: the second save is rejected with a "changed by someone else" message. | Proposed |
| A-67 | Live dashboard is exception-first: alerts, disrupted trains, then full network. | Proposed |
| A-68 | Only the station manager changes platforms at their station. | Confirmed |
| A-69 | A journey crossing regions is controlled by the region the train is currently in; the chief controller can act anywhere. | Proposed |
| A-70 | Disruptions = Incident (cause) + effects on journeys; one incident can be applied to several journeys in one action. | Proposed |
| A-71 | Declarers per disruption type as in the Section 10 propagation matrix. | Confirmed |
| A-72 | Passenger messages from templates per event, plus optional controller reason. | Proposed |
| A-73 | All channels receive disruptions from the same event stream. | Proposed |
| A-74 | Affected passengers = those tracking / saving the journey (push) + everyone viewing apps, terminals and station screens. | Proposed |
| A-75 | Route changes as skipped / added stops; full diversions deferred. | Proposed |
| A-76 | MVP handles unplanned disruptions only; planned engineering works out of scope. | Confirmed |
| A-77 | Disruption lifecycle active → resolved; reinstatement notifies the same audience as the cancellation. | Proposed |
| A-78 | Cancellation and early-termination notices include the next trains to the same destination. | Proposed |
| A-79 | Crew report incidents; the controller creates or dismisses an incident from each report. | Confirmed |
| A-80 | Station closure: station manager requests, controller approves; in an emergency the station manager closes immediately and the controller is notified. | Confirmed |
| A-81 | Network-wide incidents appear as an in-app alert banner for all passengers until resolved. | Confirmed |
| A-82 | Passenger app and terminal have an Emergency button with one-tap emergency numbers; non-urgent passenger reports deferred. | Confirmed |
| A-83 | Provider owns menu, prices, availability, lead times, orders, payment and handover; SmartRail owns train data, accounts, the order ↔ journey link and the no-show record. | Proposed |
| A-84 | MVP payment is pay on collection; online payment deferred. | Confirmed |
| A-85 | Collection stations are catering-enabled stops between boarding and destination; counter collection only where the stop is long enough or at the destination. | Proposed |
| A-86 | Cut-off = live expected arrival − provider lead time for that station and handover type. | Confirmed |
| A-87 | SmartRail automatically cancels orders when collection becomes impossible, and notifies passenger and provider. | Proposed |
| A-88 | For long delays the provider decides whether to keep or cancel. | Proposed |
| A-89 | A missed collection becomes "not collected"; no automatic transfer. | Proposed |
| A-90 | Collection confirmed by provider staff via order code / QR. | Proposed |
| A-91 | Provider receives order reference, first name, train, collection station, coach (for delivery), expected time and the passenger's phone number for active orders only, with consent at order time; the provider must not keep it after the order ends. | Confirmed (revised 5 Oct) |
| A-92 | Integration: API calls + signed events both ways, retries, duplicate protection; SmartRail keeps working when the provider is down. | Proposed |
| A-93 | Simulated provider form (independent app vs mock): decision deferred. | Open |
| A-94 | Handover by platform counter or coach delivery, chosen by the passenger. | Confirmed |
| A-95 | Uncollected orders: reminder before cut-off, 2 genuine no-shows → ordering suspended 30 days, item / value limit per order. Disruption-caused cancellations never count as no-shows. | Confirmed |
| A-96 | Notification matrix as in Section 13; in-app state is the source of truth, push is best-effort. | Confirmed |
| A-97 | Identity model as in Section 14: anonymous public use, passenger accounts, device credentials, staff accounts with scope, system credentials for the provider. | Confirmed |
| A-98 | Operations staff use two-factor authentication. | Confirmed |
| A-99 | One design system shared by all apps, with per-surface density; identical status wording, colours and icons everywhere. | Confirmed |
| A-100 | Accessibility target: WCAG 2.2 AA. | Confirmed |
| A-101 | Ops web does not queue actions while offline. | Confirmed |
| A-102 | All clients reload full current state on reconnect. | Confirmed |
| A-103 | Passenger sign-up collects email and phone; sign-in is email + password; passengers stay signed in on their device. | Confirmed |
| A-104 | Main threats and controls as in Section 17; threat model on 11 Oct. | Confirmed |
| A-105 | The 5 automated end-to-end flows as listed in Section 18. | Confirmed |
| A-106 | The system clock is controllable in tests. | Confirmed |
| A-107 | Observability as in Section 19, including ETA accuracy as a business metric. | Confirmed |
| A-108 | Data classes, visibility and retention as in Section 20; logs never contain personal data. | Confirmed |
| A-109 | SMS is a fallback channel for critical messages (saved-trip cancellation / termination, food order confirmation / cancellation); simulated in the MVP. | Confirmed |
| A-110 | Phone number shared with the provider only for an active order, with consent; masked calling (as in ride-hailing apps) is future work. | Confirmed |
| A-111 | Phone number verified once by SMS in the background after sign-up (never blocks sign-in); verification required before the first food order. | Confirmed |
| A-112 | The lead time of a platform change (time between the controller's confirmation and the scheduled arrival) is calculated automatically, and the response depends on three levels of lead time. | Proposed (the three levels were not in the text copied from the other session) |
| A-113 | A platform change with under 5 minutes' notice includes a train hold set by the controller, and a calm announcement ("the train will wait for passengers to board"). The wait is promised only when the hold is actually set; otherwise the wording asks people to move calmly and follow staff, without promising a wait. | Confirmed |
| A-114 | Station staff get a high-priority alert at the same moment as passengers (and the crew app and radio carry the hold instruction first). | Confirmed |
| A-115 | Station staff can post local notices only (for example platform crowded, keep clear of the stairs), shown on that station's own displays. They cannot change train, platform or stop data, and they cannot close a platform on their own; anything that affects trains is sent to the controller, who decides and informs the station. An emergency platform hold is deferred. | Confirmed |
| A-116 | In the crew app the driver reports an incident by choosing from a fixed list of incident types (glance-only, as in A-51). Only the guard can add a free-text explanation. Reports go to the controller, never directly to passengers (as in S10-05). | Confirmed |
| A-117 | Every delay or cancellation notification states the minutes late, the reason and the platform (and the new platform if it changed). | Proposed |
| A-118 | A push that is already out of date when the phone reconnects is not sent; the screen shows the current state with "last updated". | Proposed |
| A-119 | Alternatives offered are SmartRail trains only (A-08). If there are none, the app says so and shows the station's contact or help-desk details. | Proposed |
| A-120 | The station page gives plain-text wayfinding for the seeded stations (where the platforms are, how to reach them). Could-have. | Proposed |
| A-121 | A guest can follow one journey on their device for the day without an account and without push alerts; alerts, saved journeys and food orders need an account (A-07). | Confirmed |
| A-122 | Events sent to the catering provider carry a version number and a timestamp; duplicates and older events are ignored. | Confirmed |
| A-123 | The provider can also query a journey's current status and ETA through the API, as a fallback if events are missed. | Confirmed |
| A-124 | Events reach the provider normally within 1 minute; an alert is raised at 5 minutes (extends the backlog alert in A-107). | Confirmed |
| A-125 | Messages to and from the provider use an API key plus a signature with a timestamp (replay protection). mTLS is production hardening, deferred. | Confirmed |
| A-126 | Audit logs are append-only: application users cannot edit or delete entries. | Confirmed |
| A-127 | Who pays for food already prepared when a disruption cancels an order is a commercial matter between the railway and the provider. The MVP records the timestamps of delay and cancellation events as evidence; there is no compensation workflow. With pay on collection there is nothing to refund. | Confirmed |
| A-128 | Catering staff store and hand over food themselves, at places designated by the station. The provider may assign its own staff to a station but must give the station those staff members' details beforehand (an agreement between provider and station, not a feature in the MVP). SmartRail does not rely on station staff to hold food, and nobody waits at the platform edge. | Confirmed |
| A-129 | The system administrator can disable a crew account or device at once. It takes effect when the device next connects; a journey already running keeps working offline. | Confirmed |
| A-130 | Crew sign in with their own staff account on any registered device, so a spare tablet works immediately; the sign-in stays valid offline for the shift. If the app is unavailable, radio and the logbook remain the fallback. | Confirmed |
| A-131 | Invalid updates (impossible position, future timestamp, unknown journey, a state change that is not allowed such as "arrived" before "departed") are rejected and logged; the public sees the last good state with its age (A-26). | Confirmed |
| A-132 | Repeated authorisation failures from one account or address raise an alert. | Confirmed |
| A-133 | Placing a food order is idempotent (client key): a double tap or retry never creates two orders; after a reconnect the app fetches the real order state. | Confirmed |
| A-134 | Controller messages need acknowledgement from the driver or guard. The driver acknowledges only when the train is stationary; critical instructions still go by radio. | Confirmed |
| A-135 | Crew incident reports the controller has not handled stay highlighted on the dashboard, with no automatic public action. | Confirmed |
| A-136 | Personal data is encrypted in transit (TLS) and at rest. | Confirmed |
| A-137 | Tests include malformed, duplicate and out-of-order events, for positions and for provider events (A-122). | Confirmed |
| A-138 | Station managers get a high-priority alert on the operations web app at the same moment as the public for cancellations, early terminations and platform changes affecting trains at their station (extends A-114). | Confirmed |
| A-141 | The same crew app runs in two modes: cab mode on a registered cab tablet (operates the journey, sends GPS) and phone mode (today's journeys and train sets, taps, incident reports, messages). The mode follows the device registration in the seed data. | Proposed |
| A-142 | A cab tablet is registered to a train set and holds a device credential, like the displays. The driver signs in on it with their own staff account; the sign-in clears at End journey or after an idle limit, and nothing personal stays on the device. | Proposed |
| A-143 | On Start journey the system compares the journey's assigned train set with the tablet's train set; a mismatch warns the driver and alerts the controller (Should). Staff ID + PIN quick sign-in on a registered device is a Should; normal sign-in applies until then. | Proposed |
| A-144 | The cab tablet connects over its own 4G data SIM; positions and actions go over HTTPS, controller messages over the same event stream as the other apps, with the offline queue (A-55) and a full state reload on reconnect (A-102). Private APN, device management and onboard routers are production items, not MVP. | Proposed |
| A-145 | Timestamps on queued actions use GPS time or a server clock offset taken at sign-in, so a wrong tablet clock does not make a train look early or late. | Proposed |

## Deferred so far

| Item | Reason |
|------|--------|
| Ticket sales and seat booking | Needs payment, seat inventory, holds and refunds; payment ownership already unresolved for catering. |
| Map view of train position | Needs track geometry and a map provider; the line diagram meets the need. |
| Tracking a train on behalf of someone else | Not needed in this scope. |
| Automatic one-change journey planning | Could-have; direct trains first. |
| Train splitting / joining, driver change mid-journey, train set swap mid-day | Real but rare; adds model complexity without changing the core passenger journeys. |
| On-train GPS unit integration | Future source (better accuracy, no driver phone battery drain); MVP uses driver app GPS through a source-agnostic API. |
| ETA recovery prediction (trains making up time) | MVP carries the current delay forward. |
| Separate arrivals board | Replaced by arriving trains on the large information screen. |
| Integration with station PA (audio) system | MVP speaks announcements via text-to-speech on the large screen; real PA integration later. |
| Sinhala / Tamil language rotation on boards | English only in MVP; built ready for translation (A-05). |
| Food ordering on the station terminal | Needs identity and payment on a shared public device; ordering stays in the mobile app. |
| Terminal accessibility extras (large-text mode, spoken output) | Recorded; after core terminal features. |
| Crew app for assistant driver, ticket inspector, station master, security officers | MVP covers driver and guard; station master is a strong future data source for actual times. |
| Customer service role (complaints, feedback) | No feedback feature in MVP. |
| Reporting viewer role and reports | Lower priority than live operations. |
| Separate customer information officer, incident and service desks | Folded into the controller role for MVP. |
| Planned engineering works | Out of scope; would use effective-dated timetable changes. |
| Full route diversions onto another line | Skipped / added stops cover most cases. |
| Non-urgent passenger issue reporting | Needs a verification workflow; deferred with customer service. |
| Knock-on delays from train set reuse | A-14 simplifies train set use. |
| Online payment for food orders | Pay on collection in MVP; provider would own online payment. |
| Group food orders | One passenger, one order in MVP. |
| Masked phone calls between provider and passenger | MVP shares the real number for active orders only, with consent. |
| Ticket purchase confirmations by SMS | Ticketing deferred; phone number already collected for it. |
| Staff-first window (staff alerted about 60 seconds before the public, with a 15-second acknowledgement timeout) | Deferred by the author. Staff are alerted at the same moment as passengers (A-114); the calm announcement and the train hold handle the crush risk. |
| "Delay Likely" / unconfirmed status shown to passengers | Not needed. Lateness is shown as exact minutes against the schedule from the driver GPS (C-3). |
| Lift outage tracking, step-free routing, staff escort tasks, haptic alert patterns, tactile terminal buttons, staffed help point | Lifts are treated as a minor issue (stairs and footbridges exist, staff can help); not in the MVP. WCAG 2.2 AA (A-100) still applies. |
| Station staff handheld tasks for assisting passengers | Part of the deferred station crew app. |
| Customer-service access to passenger data (case-based access, masking, delegated consent, dual-control break-glass, caller identity checks) | Customer service is deferred; revisit with it. |
| Biometrics, NFC cards, hardware-bound keys and device management for crew devices | Overkill for the MVP; A-129 and A-130 cover lost devices and spares. |
| Legal hold on passenger records and separate encryption keys | Deferred; the PDPA applicability check is still open (Section 20). |

---

## AI usage log entries from this analysis

| Date | Activity | AI suggestion | My decision | Why |
|---|---|---|---|---|
| 1 Oct | AI first pass, Section 2 | Stated / implied / missing analysis | Accepted with additions | Added fares, season tickets, seat reservation costs and coach classes, which AI missed. |
| 2 Oct | Fares scope | Fare info as Could-have; defer booking | Accepted: read-only fares | Booking needs payment and inventory; too large for MVP. |
| 2 Oct | AI first pass, Section 3 | Live tracking placed only on board | **Changed** | Passengers also need tracking while waiting; reframed as ride-hailing-style tracking with two modes. |
| 2 Oct | Section 3 | Optional passenger GPS (nearest station, boarding detection) | **Rejected** | Train position comes from the train / driver device; passenger GPS adds privacy risk with no essential use. |
| 2 Oct | Section 3 | Auto-switch to on-board mode at departure | **Rejected** | Would wrongly show a passenger who missed the train as on board. Chose manual tap; accepted AI's follow-up prompt for the forgot-to-tap case. |
| 2 Oct | Section 3 | "Someone tracking another person" as an actor | **Rejected** | Not needed in this scope. |
| 2 Oct | Section 3 | Customer service under passenger experience | **Moved** | Belongs with the operations platform (Section 9). |
| 2 Oct | Section 3 | Journey planner with route finding | **Simplified** | Direct trains stopping at both stations; AI added stop-order and service-date rules. |
| 3 Oct | AI first pass, Section 4 | Plan vs operation split; glossary resolving "train"/"journey" overload | Accepted | Keeps delays out of the timetable; one meaning per term across docs, API and code. |
| 3 Oct | Section 4 | Suggested distance-based fares as easier to maintain | **Rejected** | Chose fixed price per station pair; distances still kept for position estimation. |
| 3 Oct | Section 4 | Real vs fictional station names | Chose real Sri Lankan stations | Realistic demo; operator and timetable stay fictional. Station data must be verified against an authoritative source, not AI output. |
| 3 Oct | AI first pass, Section 5 | Obtain / calculate / communicate / update pipeline; driver GPS + simulator | Accepted | Driver GPS for MVP; added on-train GPS unit as the future source (accuracy, battery). |
| 3 Oct | Section 5 | "On time" = up to 5 min late (tolerance band) | **Rejected** | Show exact minutes against the schedule ("3 min late"), more useful to passengers. |
| 3 Oct | Section 5 | "Never depart early" as an absolute rule | **Changed** | Trains do run early in practice; system shows early running. Rule narrowed to *predictions* only. |
| 3 Oct | Section 5 | Asked whether controller or GPS wins | Decided: controller highest | Controller can correct GPS faults; driver buttons second, GPS third. |
| 3 Oct | AI first pass, Section 6 | Display responsibilities by location; offline behaviour | Accepted with changes | Added train type (slow / express) to boards and stopping stations to platform display. |
| 3 Oct | Section 6 | Separate arrivals board | **Replaced** | Real stations use large information screens inside the station: arriving trains + announcements + sudden changes. |
| 3 Oct | Section 6 | (Own idea) show announcements visually while spoken | Added | AI linked it to accessibility for deaf / hard-of-hearing passengers and split messages into automatic and manual. |
| 3 Oct | Section 6 | Record spoken announcements as missing and defer | **Changed** | Spoken announcements are required for visually impaired passengers; AI proposed text-to-speech on the large screen as an MVP-sized solution. |
| 4 Oct | AI first pass, Section 7 | Shared-device rules (knows its station, forgets everything), kiosk interaction model | Accepted | |
| 4 Oct | Section 7 | Food ordering on terminal: information only | Accepted, refined | Terminal states catering is available and directs passengers to the mobile app to order. |
| 4 Oct | Section 7 | "Available passenger services" ambiguous | Decided | Catering information and assistance. |
| 4 Oct | AI first pass, Section 8 | Safety (glance-only while moving), "driver reports, controller decides", radio for safety-critical | Accepted | Radio also kept as backup channel. |
| 4 Oct | Section 8 | Asked: driver, guard or both | Decided: both | Guard can do inputs while moving, reducing driver distraction. |
| 4 Oct | Section 8 | (Asked AI) other operational roles | Reviewed list | Assistant driver, ticket inspector, station master, security deferred; catering staff external; infrastructure staff out of scope. |
| 4 Oct | AI first pass, Section 9 | Six roles with scopes; permission matrix; exception-first dashboard | Accepted with changes | |
| 4 Oct | Section 9 | Controller and station manager can both change platforms | **Changed** | Only the station manager changes platforms at their station. |
| 4 Oct | Section 9 | Single controller role | **Changed** | Added regional controllers under a chief controller (real railway structure); AI raised cross-region ownership question. |
| 4 Oct | Section 9 | Customer service role | Deferred | No feedback feature in MVP. |
| 4 Oct | AI first pass, Section 10 | Incident (cause) + effects model; propagation matrix | Accepted with additions | |
| 4 Oct | Section 10 | Incidents created by controller only | **Changed** | Driver and guard can report incidents; controller confirms or dismisses. |
| 4 Oct | Section 10 | (Own idea) passenger incident reporting to prevent disasters | Discussed | AI raised risks (too slow for emergencies, false reports, response expectation); proposed Emergency call button now, verified reporting later. **Accepted.** |
| 4 Oct | Section 10 | Who closes a station | Decided | Station manager requests, controller approves. |
| 4 Oct | Section 10 | Resolution notices | Decided | Network-wide incidents shown as an in-app alert banner. |
| 5 Oct | AI first pass, Sections 11–12 | Data ownership table, two-way integration, ordering flow, answers to the 11 questions | Accepted with changes | |
| 5 Oct | Sections 11–12 | Single handover choice | **Changed** | Both platform counter and coach delivery, by passenger preference. |
| 5 Oct | Sections 11–12 | Fixed 45-min cut-off | **Changed** | Cut-off depends on collection station and distance from the provider's kitchen; AI turned this into provider-owned lead time per station. |
| 5 Oct | Sections 11–12 | Pay on collection | Accepted, **raised new risk** | Own question: what if the order is not collected? AI proposed reminder, no-show strikes and item limits; decision pending. |
| 5 Oct | Sections 11–12 | Uncollected-order options | Decided: reminder + no-show strikes + order limit | Fairness rule: disruption-caused cancellations never count. |
| 5 Oct | AI first pass, Sections 13–16 | Notification matrix, identity model, "one design system, five densities", per-app offline behaviour | Accepted | |
| 5 Oct | Section 14 | Phone number + SMS one-time code as an option | **Rejected for sign-in** | SMS codes can be delayed; busy passengers need fast sign-in. Email + password; email and phone collected at sign-up. |
| 5 Oct | AI first pass, Sections 17–20 | Threat list, 5 E2E flows, observability, data classification and retention | Accepted | |
| 5 Oct | Section 20 | Asked: what is the phone number for (or make it optional) | Purpose defined | SMS fallback for critical messages, order confirmations, provider can call about an active order. AI added consent, active-order-only sharing and masked calling as future work. |
| 5 Oct | Section 14 | One-time background phone verification | Accepted | Prevents SMS and provider calls reaching a stranger, without slowing sign-in. |
| 5 Oct | Stakeholder simulations (LangGraph + Gemini agents, run locally) | Station manager vs controller debate on platform authority | **Earlier decision reversed** | Controller owns train movement and platform assignment; station sends information and is informed first. Simulation evidence changed my Section 9 decision. |
| 5 Oct | Simulation conflict C-2 (lead time) | AI: three lead-time levels; controller warning or optional hold for very late changes | **Extended** | My idea: for changes under 5 min, announcements politely say the train will wait while passengers cross the bridge, so they don't panic; staff guide them. AI added: only promise the wait when the controller has actually set the hold. |
