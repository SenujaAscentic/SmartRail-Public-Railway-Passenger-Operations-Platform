# Assumptions Log

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
