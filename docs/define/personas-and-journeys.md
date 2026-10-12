# SmartRail – Personas and User Journeys

**Status:** Revised 6 Oct 2026 (v2: journey maps in standard format, catering provider persona added).
**Sources:** stakeholder simulations (22 sessions) and the as-is "day in the life" document (d1–d5). Personas are **synthesised from AI simulations, not real people**; times and frequencies are unverified hypotheses.
**Traceability:** capability IDs (P-, O-, C-, D-, B-, F-, S-) refer to `define/moscow.md`.

**How to read the journey maps**
- Each journey has a **scenario** with a happy path and an unhappy (disruption) path, because a railway app is judged on bad days.
- Each **stage** shows what the person does, where (touchpoint), how they feel **as-is → to-be** on a scale from −2 (very stressed) to +2 (confident), the pain point today, and what SmartRail changes.
- **Moments of truth** are the stages where SmartRail wins or loses the person's trust; they become the priority acceptance tests.

---

## 1. Personas

### Primary personas (the MVP is designed for these)

| Persona | Role | Main goal | Biggest pain today | Key capabilities |
|---|---|---|---|---|
| **Kasun Perera**, 34 | Daily commuter, Gampaha → Colombo Fort, 06:40 | Decide at home, never miss the train | False or missing information; RDMNS has no platforms and no background alerts | P-1, P-3, P-4, P-6, P-7 |
| **Fathima Rizwan**, 58 | Occasional traveller to Kandy; knee problems | Travel without stress, check things herself | Small abbreviated boards, no alerts, hunting for staff | P-1, P-2, P-9, D-1–D-3, S-5 |
| **Sunil Bandara**, 49 | Train driver, 22 years on the Main Line | Drive safely, no data entry | Reports lost in a phone chain; no screen in the cab today | C-1–C-4, C-6, C-7 |
| **Chamari Jayasinghe**, 44 | Station manager, Maradana | Keep crowds safe | Late platform changes cause surges; she learns with the crowd | O-5, O-9, D-3, S-2 |
| **Ruwan Dissanayake**, 51 | Controller, Main Line | One decision, everyone informed | Learns about problems 5–10 min late; distrusts screens | O-2–O-8, B-6 |

### External partner persona

| Persona | Role | Main goal | Biggest pain today | Key capabilities |
|---|---|---|---|---|
| **Dilani Fernando**, 39 | Operations manager, *RailBites Catering* (fictional provider) – kitchens serving Polgahawela and Kandy | Food ready exactly when the train arrives | Prepares by the printed timetable; delays mean cold food and waste; phone orders with vague timing | F-1–F-4, P-8 |

Dilani never uses a SmartRail screen. She uses her own company's system; SmartRail reaches her **through the integration**. She is included because the integration is a required part of the MVP (brief section 23) and needs a user whose needs it serves.

### Secondary personas
Guard (does most crew-app input: C-4, C-5, C-6) · chief controller and regional controllers (deferred with more lines, 9 Oct) · system administrator · tourist (partly covered by Fathima).

Full persona cards (background, devices, frustrations, quotes) are in the appendix.

---

## 2. Journey maps

### 2.1 Kasun – daily commuter

**Scenario:** weekday morning to work; in the evening his train's platform is changed with 4 minutes' notice, and the next day his train is cancelled.

| Stage | Doing | Touchpoint | Feeling as-is → to-be | Pain today | With SmartRail | IDs |
|---|---|---|---|---|---|---|
| 1. Check before leaving (06:15) | Checks his usual train over tea | Phone | −1 → +2 | RDMNS grey box, no platform | Saved trip: "06:40 · On time · Platform 2 · updated 30 s ago" | P-6, P-1, P-4 |
| 2. Change at home (06:24) | Decides when to leave | Push notification | −2 → +1 | No warning; finds out on the platform | Push: "8 min late – signal fault near Ragama"; leaves later | P-7, O-4 |
| 3. At the station | Finds his platform | Platform display, app | −1 → +2 | Garbled PA, asks regulars | Display and app say the same thing | D-2, B-5 |
| 4. Waiting | Waits for the train | App (waiting mode) | −1 → +1 | Watches the track | "Passed Ragama · arrives in 4 min" | P-3 |
| 5. On board | Plans arrival | App (on-board mode) | 0 → +2 | No information | "Next: Kelaniya · Fort 07:33" | P-3 |
| 6. Evening platform change (unhappy path) | Rushes to new platform | Push, large screen, announcement | −2 → +1 | Crowd surge on stairs | "Platform 5 (was 2) – the train will wait for passengers" | P-7, O-5, D-3 |
| 7. Cancellation (unhappy path) | Needs a new plan | App | −2 → +1 | "Indefinite delay", phones home | "Cancelled – next: 17:45 (Platform 3), 18:05" | P-4 |

**Moments of truth:** stage 2 (does the alert arrive before he leaves?) and stage 6 (does the announcement calm the crowd?).
**Not changing:** paper ticket; RDMNS still exists.

### 2.2 Fathima – occasional traveller

**Scenario:** visiting her daughter in Kandy; her platform is changed while she waits.

| Stage | Doing | Touchpoint | Feeling as-is → to-be | Pain today | With SmartRail | IDs |
|---|---|---|---|---|---|---|
| 1. Plan (day before) | Chooses a train | App without account | −1 → +1 | Asks friends which is "the good one" | Search shows Express / Slow, times, 2nd / 3rd class price | P-1, P-9, S-5 |
| 2. Arrive at Fort (45 min early) | Finds departure | Main board, large screen | −1 → +1 | Small flickering boards, abbreviations | "10:35 Kandy · Express · Platform 4 · On time" in plain words | D-1, D-3 |
| 3. Confirm the train | Checks it is hers | Platform display | −1 → +2 | Shows ticket to staff | "Stops: Ragama, Gampaha, Polgahawela, Peradeniya, Kandy" | D-2 |
| 4. Platform change (unhappy path) | Moves to new platform | Announcement, display | −2 → +1 | Rushes on stairs with bad knees | "Now Platform 5 – the train will wait. Assistance is available from staff." | O-5, D-3 |
| 5. On board | Watches progress | App line diagram | −1 → +1 | Worries about her stop | Train moving on the line; "Kandy in 25 min" | P-2, P-3 |
| 6. Food | Wants a meal | App → provider hotline | 0 → +1 | Buys whatever is on the platform | "Food at Polgahawela" → menu → one-tap call; kitchen knows the live arrival time | P-8, F-2, F-3 |

**Moments of truth:** stage 3 (can she confirm the right train herself?) and stage 4 (does the platform change frighten her or reassure her?).
**Not changing:** no exit guidance or station facilities information; no ticket purchase.

### 2.3 Sunil (with the guard) – driver

**Scenario:** morning Main Line run; a signal fault, a hold instruction and a tunnel without signal.

| Stage | Doing | Touchpoint | Feeling as-is → to-be | Pain today | With SmartRail | IDs |
|---|---|---|---|---|---|---|
| 1. Sign on (04:30) | Logbook, driver's pack, radio | Shed, paper | 0 → 0 | — (unchanged) | Plus sign-in on cab device: "Today: 05:20 Maradana → Kandy" | C-1 |
| 2. Depart | Starts the run | Crew app | 0 → +1 | — | One tap: Start journey (tracking on) | C-2 |
| 3. Running | Drives, watches signals | Glance view | 0 → +1 | No information on screen (none exists) | "Next: Ragama 05:41 · 2 min late"; no typing while moving | C-3 |
| 4. Signal fault (unhappy path) | Radios signalman | Radio; guard's app | −2 → −1 | Report lost in phone chain | Radio first; guard reports "Signal fault"; controller sees it at once | A-50, C-5, O-6 |
| 5. Instruction | Receives hold order | Crew app + radio | −1 → +1 | Verbal or written order only | "Hold at Platform 5 for passengers", acknowledged when stopped | C-6, O-5 |
| 6. Tunnel | No signal | Crew app | — → 0 | — | Actions queued, sent later with real times | C-7 |
| 7. Sign off | End of run | Crew app, logbook | 0 → +1 | — | End journey (tracking off; no location off duty) | C-2, A-29 |

**Moments of truth:** stage 3 (does the app stay out of his way?) and stage 4 (does his report reach the controller without a phone chain?).
**Not changing:** signals, caution orders, speed restrictions, logbook and defect book.

### 2.4 Chamari – station manager

**Scenario:** Friday evening peak at Maradana; a 4-minute platform change and an escalator fault.

| Stage | Doing | Touchpoint | Feeling as-is → to-be | Pain today | With SmartRail | IDs |
|---|---|---|---|---|---|---|
| 1. Morning check (06:00) | Checks boards | Platforms, ops web | −1 → +1 | Compares boards with printed timetable | Boards fed by the same live source; ops view of Maradana only | O-1, D-1 |
| 2. Special day | Poya timetable | Boards | −1 → +1 | Paper special timetable | Boards follow the Poya calendar automatically | B-1 |
| 3. Late platform change (unhappy path, 18:02) | Manages the crowd | Ops alert, large screen | −2 → 0 | Learns with the crowd; grabs the PA | Alert at the same moment as passengers; screen says the train will wait; staff to the stairs | O-5, D-3 |
| 4. Escalator fault | Informs passengers and control | Ops web | −1 → +1 | Paper notice, radio | Posts announcement; station notice to the controller | O-9, S-2 |
| 5. Handover (14:00) | Hands over shift | Logbook | 0 → 0 | — (unchanged in MVP) | Digital handover note is a Could | – |

**Moments of truth:** stage 3 (does she hear before the crowd moves?).
**Not changing:** platform decisions stay with the controller; PA not integrated.

### 2.5 Ruwan – Main Line controller

**Scenario:** a train stalls near Kelaniya at peak; a following train is cancelled and another picks up its passengers.

| Stage | Doing | Touchpoint | Feeling as-is → to-be | Pain today | With SmartRail | IDs |
|---|---|---|---|---|---|---|
| 1. Take the desk (05:45) | Learns current state | Dashboard | −1 → +1 | Verbal "hot list" | Problems first: stale GPS, open incidents, crew reports | O-2 |
| 2. Trust check | Checks the data | Dashboard | −2 → +1 | Screens show trains "arrived" too early | Every value shows source and age | O-3 |
| 3. Train stops (unhappy path, 07:38) | Detects the problem | Alert | −2 → +1 | Learns at 07:45–07:48 via phone chain | 07:41 "Unscheduled stop" alert + guard's report; passengers already see the delay growing | O-8, O-6, B-4 |
| 4. Decide | Sets delay and reason | Dashboard | −1 → +2 | Phones three stations | One action reaches every app, display, crew device and the provider in seconds | O-4, B-6, F-2 |
| 5. Recover | Cancels 07:50, adds stops to 08:05 | Dashboard | −1 → +1 | Phones stations and signal boxes | Cancel + add stops; passengers and boards update | O-4 |
| 6. Handover (14:00) | Hands over | Logbook + audit log | 0 → +1 | Handwritten Daily Occurrence Book | Audit log is a full record of actions | O-10 |

**Moments of truth:** stage 2 (does he trust the screen?) and stage 4 (does one action really reach everyone?).
**Not changing:** signalling, section clearance, relief engines.

### 2.6 Dilani – catering provider (external)

**Scenario:** lunchtime service at Polgahawela; one train is delayed, another has its Polgahawela stop skipped.

| Stage | Doing | Touchpoint | Feeling as-is → to-be | Pain today | With SmartRail | IDs |
|---|---|---|---|---|---|---|
| 1. Plan the day | Plans cooking | Own system | −1 → +1 | Uses the printed timetable | Receives SmartRail's catering stations and journeys | F-1 |
| 2. Publish menu | Updates menu and availability | Own system → SmartRail | 0 → +1 | Menu only at the counter | Menu, availability, hotline and hours appear in the passenger app | F-3, P-8 |
| 3. Take phone orders | Answers calls | Hotline | −1 → +1 | Callers don't know their arrival time | Callers read "Train 1005 · arriving 10:52 · coach" from the app | P-8 |
| 4. Train delayed (unhappy path) | Times the cooking | Event log | −2 → +2 | Food goes cold; finds out from staff calls | Live ETA update: "1005 Polgahawela expected 11:07 (+15)" | F-2 |
| 5. Stop skipped (unhappy path) | Stops cooking | Event log | −2 → +1 | Food wasted | "1007 will not stop at Polgahawela" arrives in seconds | F-2, O-4 |
| 6. SmartRail unreachable | Keeps working | Own system | — → 0 | — | Events are retried; nothing lost when the connection returns | F-2 |

**Moments of truth:** stage 4 (does the ETA arrive in time to change cooking?) and stage 5 (is waste avoided?).
**Not changing:** in-app ordering, payment and collection are in the next release; payment stays cash at handover.

---

## 3. Gaps found while writing the journeys

| Gap | Persona | Decision |
|---|---|---|
| Exit and station facilities information | Fathima | Out of MVP (recorded) |
| Digital shift handover | Chamari, Ruwan | Already Could |
| "Assistance is available from staff" in announcements | Fathima, Chamari | Add to announcement templates (no new feature) |
| Hotline wording may be unclear to elderly users | Fathima | Check at wireframe review |
| Provider needs a list of journeys calling at its stations, not only events | Dilani | Covered by F-1 (catering stations and journeys); confirm in API design |

---

## Appendix – persona cards

**Kasun Perera, 34 – daily commuter.** Accounts clerk in Colombo; 06:40 Gampaha → Colombo Fort, back around 17:30; school pickup two evenings a week. Mid-range Android, weak signal in places. *"Five minutes is the difference between getting on and missing it. Don't tell me 'On time' when it isn't."*

**Fathima Rizwan, 58 – occasional traveller.** Retired teacher; Colombo Fort → Kandy a few times a month; knee problems make stairs slow. Uses her phone mainly for calls and messages; avoids apps that "feel built for engineers". *"I come 45 minutes early because I don't trust what I'll find."*

**Sunil Bandara, 49 – train driver.** 22 years on the Main Line, starts at the Maradana running shed. Radio, paper driver's pack, logbook; no screen in the cab today. *"Don't give me a screen to tap while I'm driving."*

**Chamari Jayasinghe, 44 – station manager.** Busy interchange with footbridges; shift 06:00–14:00. Radio, personal phone, logbook, PA microphone. *"We on the station floor face the crowd when things go wrong."*

**Ruwan Dissanayake, 51 – Main Line controller.** Morning shift, whole Main Line (one line in the MVP; regional controllers come with more lines). Wall track diagram, radio, telephone, Daily Occurrence Book; does not trust digital screens. *"I decide what the public sees. I won't put my name on a guess."*

**Dilani Fernando, 39 – catering provider.** Operations manager at the fictional RailBites Catering; kitchens serving Polgahawela and Kandy; cash at handover. *"If I knew when the train would really arrive, nothing would go cold or to waste."*
