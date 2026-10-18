# SmartRail – Design Handoff (UX/UI)

**From:** Senuja (product owner and developer) · **Date:** 16 Oct 2026, **updated 18 Oct 2026** · **Demo:** Tue 20 Oct 2026
**What changed on 18 Oct:** build order is now passenger app → driver app → station displays → ops web (last); Flutter apps use Material 3 with token colours (rules 11–13); the passenger stop list shows calling points only.
**Attach with this brief:** `shared/tokens.json` (design tokens) and `shared/status-fixtures.json` (the exact status texts).

---

## 1. What SmartRail is

A real-time railway information and operations platform for a **fictional operator** on **one real Sri Lankan line**: the Main Line corridor **Colombo Fort ↔ Kandy**, both directions. One event stream keeps every screen in sync within 5 seconds.

**Use only fictional branding.** Do not imitate Sri Lanka Railways' name, logo or colours. Real station names are fine.

Stations in order (Fort → Kandy): Colombo Fort, Maradana, Kelaniya, Ragama, Gampaha, Veyangoda, Polgahawela, Rambukkana, Kadugannawa, Peradeniya, Kandy.
Trains: express (stops at Fort, Maradana, Gampaha, Polgahawela, Peradeniya, Kandy; passes the rest) and slow (all stops). Example: **"1001 Kandy Express"**.

## 2. Who uses it

| Persona | Role | App | What matters most |
|---|---|---|---|
| Ruwan | Railway controller (whole line) | Ops web, desktop | See problems first; trust every value; one action reaches everyone |
| Chamari | Station manager (one station) | Ops web | Post announcements to her station's screens |
| Sunil | Train driver | Driver (crew) app: phone + cab tablet | Glance-only while driving; big targets; works offline |
| Kasun | Daily commuter | Passenger app, phone | Exact lateness, never a false "On time"; alerts before leaving home |
| Fathima | Occasional traveller | Passenger app + station screens | Confirm she's at the right platform for the right train |
| Dilani | Catering provider | Her own system (not designed here) | — |

## 3. Design rules (non-negotiable)

1. **Status = icon + text + colour.** Never colour alone (WCAG, Q-4). Icons and colours are in `tokens.json → status`.
2. **Status wording comes only from `status-fixtures.json`.** Examples: "On time", "3 min late", "2 min early", "1 h 15 min late", "Platform 5 (was 2)", "Cancelled", "Terminates at Rambukkana".
3. **Every live value shows its age**, e.g. "updated 30 s ago". After 2 min: "Last seen 07:38 near Kelaniya". After 10 min: "Live location unavailable – scheduled times shown".
4. **Ops screens also show the source** of each value: GPS / Driver / Controller / Schedule, plus age ("GPS · 15 s ago"). Controller overrides are labelled as such.
5. **No predictions or vague labels.** Never "Delay likely". Show only facts in whole minutes.
6. **"The train will wait for passengers"** appears only when the controller has actually set a hold.
7. **Contrast WCAG 2.2 AA** in light and dark themes. Visible focus states. Full keyboard use on web.
8. **Times use tabular numerals** (Noto Sans / Noto Sans Mono). 24-hour local time (Asia/Colombo).
9. **Announcements are plain text** (max 200 characters), never formatted.
10. **Themes:** the crew app and station displays are dark; the passenger app and ops web follow the system setting.
11. **Flutter apps (passenger, driver) use Material 3 components, but with the token colours.** Build the colour scheme from `tokens.json`; never let Material generate a palette from a seed colour (that would change the checked contrast). Status colours are an extra theme layer on top. Fonts are Noto Sans / Noto Sans Mono, bundled in the app.
12. **Icons: built-in Material icons**, mapped from the token icon names (e.g. clock-alert → schedule, hand-stop → pan_tool, arrows-exchange → swap_horiz, check-circle → check_circle, x-circle → cancel).
13. **Passenger stop lists show calling points only** (stations where the train stops). Stations an express passes are not listed; the line diagram may show them as small unlabelled dots. "Passed stops marked" means stops the train has already gone through.

Type sizes, spacing, radius and touch-target minimums per surface are in `tokens.json` (ops 32 px targets; passenger 48 px; crew 64 px; displays readable at 10 m).

## 4. Screens, in priority order

### Priority 1 – Passenger app (Flutter, Material 3). Phone 390 × 844. **Building now (wireframes done).**
- **Search:** from / to (type-ahead), date. Results: departure and arrival (scheduled + expected), train type, departure platform, status. Empty state: "No direct trains between these stations on this date".
- **Train details + line diagram:** all calling points with times and platforms (rule 13), passed stops marked, train marker between stations, today's train set (traction, coach classes).
- **Live tracking:** *waiting* mode (last passed station, expected arrival at my station) and *on-board* mode (next stop, arrival at destination). After departure: prompt "Are you on board?" (Yes / No, I missed it → next 3 trains).
- **Disruption states:** cancelled + next 3 trains; terminated early + alternatives; platform change "Platform 5 (was 2)"; skipped / added stop; stale and lost data.
- **Account:** sign up (name, email, phone, password), sign in, delete account. **Saved trips** (up to 5) on the home screen with today's status, including "Does not run today".
- **Catering info:** "Food available at Polgahawela", menu preview, **Call to order** (hotline + opening hours), "Menu unavailable" state, "Orders are handled by RailBites Catering".

### Priority 2 – Driver (crew) app (Flutter, Material 3, dark theme)
- **Phone mode** (portrait): today's assigned journeys in order (train set, time, route, departure platform). No journey actions.
- **Cab mode** (Android tablet, **landscape**): sign-in, **Start journey**, then the **glance view**: next stop, scheduled vs expected time, delay in very large text. **Inputs disabled while moving** (show why).
- Large **Arrived / Departed** buttons (≥ 64 px) when stopped.
- **Controller instructions** with sound/vibration and one-tap acknowledge, e.g. "Hold at Platform 5 for 3 min for passengers".
- **Offline banner:** "Offline · 3 actions waiting"; a pending tap shows "Arrived 10:12 · waiting to send".
- At the last stop: prompt "End journey".

### Priority 3 – Station displays (order after the passenger app still to be confirmed) (web, dark, 1920 × 1080, readable at 10 m)
- **Main departures board:** next 10 departures: time, destination, train type, platform, status. Cancelled trains stay until their time has passed.
- **Platform display:** next train (destination, type, expected time, remaining stops after this station, scrolling if long) + the following 2. Non-stopping train: "Next train does not stop here – stand back from the platform edge". After a change away: "Train 1520 to Kandy now departs from Platform 5".
- **Large information screen:** arriving trains + announcements area (priority emergency > disruption > general); automatic announcements for platform changes and cancellations, including "Assistance is available from staff".
- **Offline:** after 2 min, banner "Live information unavailable – check announcements or ask staff", scheduled times labelled "scheduled".

### Priority 4 – Ops web (last). Desktop 1440 px.
A technical skeleton exists (sign-in, session refresh, a bare live page). The screen design below is a starting point that Senuja will revisit before it is built.

**4a. Staff sign-in.** Staff number + password. Errors as text. One generic error for wrong details; a separate message for "locked for 15 minutes".

**4b. Dashboard (problems first, US-O2)**
- **Top: alerts panel**, newest and most severe first. Types: stale/lost GPS, unacknowledged instruction, catering provider unreachable.
- **Then: disrupted trains** sorted by lateness (on-time trains don't appear here).
- **Then: line view.** The 11 stations as a line, both directions, with every running train as a marker. Each marker shows train number, delay status, source and age.
- Filters: section (from/to station) and station.
- Live updates; a new alert visible within 5 s.

**4c. Journey detail + controller actions (US-O3, US-O4)**
- Header: "1001 Kandy Express · Colombo Fort → Kandy", status, delay with source and age, tracking freshness.
- Stop list: scheduled, expected and actual times; platform (and previous platform); stop status (Scheduled, Arrived, Departed, Passed, Skipped).
- Actions: **Delay** (minutes + reason code + optional note), **Hold** (minutes), **Cancel / Reinstate**, **Terminate early** at a station, **Skip stop / Add stop**, **Change platform**.
- **Cancel, Terminate and Skip need a confirmation screen showing what will be affected** (stations, displays, catering).
- **Conflict state:** if someone else changed the journey meanwhile: "Changed by R. Perera a moment ago – reload".
- Warning state when GPS contradicts a controller override (keep / clear).

**4d. Platform change dialog (US-O5 ★ – the most safety-critical screen)**
- Choose new platform → the dialog shows **lead time** = expected arrival − now, as one of three levels:
  - **> 15 min Normal:** normal update everywhere.
  - **5–15 min Short notice:** also high-priority station alert, repeated announcement.
  - **< 5 min Late:** a clear warning before confirming, plus an option to **set a hold** at the new platform. Only with a hold do screens say "The train will wait for passengers".
- **Conflict warning:** "Platform 5 is expected to be occupied by 1520 at 18:04" (warn, don't block).
- Preview what passengers, displays and crew will see, then Confirm.

**4e. Messages to crew (US-O7):** template or short text; status per message: sent → delivered → acknowledged; unacknowledged after 2 min is highlighted.

**4f. Station announcements (US-O9, station manager only):** text (≤ 200 chars, counter), type (general / disruption / emergency), start and end time, own station only.

**4g. Audit log (US-O10):** read-only table filtered by journey, user and time.

## 5. States to design for every screen
Loading · empty · live · stale (> 2 min) · lost (> 10 min) · offline (the device itself) · error · permission denied (e.g. a station manager on another station: "You can only post to your own station").

## 6. Out of scope (don't design)
Ticketing, in-app food ordering, multiple lines or interchanges, regional / chief controllers, guard role, crew incident reports, unscheduled-stop alerts, admin screens, Sinhala/Tamil text (the font supports them later).

## 7. What I need back
1. **Done:** lo-fi wireframes for the passenger app (saved in `docs/design/passenger/`).
2. **Next (optional, small):** a passenger **component sheet**, Material 3 components with token colours, light and dark: StatusBadge, AgeLabel / FreshnessBanner, JourneyCard, StopRow, LineDiagram, empty and error states, each in all its states. Save to `docs/design/passenger/components/`. No full hi-fi pass: the Flutter code is built from wireframes + tokens + fixtures.
3. Then lo-fi wireframes for the driver app, then station displays, then the ops web (4b, 4c, 4d first).
4. Name reusable components (StatusBadge, SourceAgeLabel, FreshnessBanner, StopRow, LineView, ConfirmImpactDialog…) and say which token each uses, so the web and Flutter code can map them directly.
5. Flag anything in this brief that is ambiguous or conflicts with a rule above, rather than guessing.
