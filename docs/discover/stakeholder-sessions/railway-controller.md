# Railway Controller

## 2026-10-04T10:35:12.518Z - Who decides platform changes, and how early must they be announced?

Roles: station-manager, railway-controller
Transcript: transcripts/station-manager+railway-controller-2026-10-04T10-35-12-516Z.md

### Needs

*   **Station Manager:**
    *   15 to 20 minutes minimum lead time to reroute passengers safely across overhead footbridges (specifically for elderly passengers and those with limited mobility/in wheelchairs).
    *   Local authority to reject platform changes initiated with insufficient lead time to prevent safety hazards and crowd stampedes.
    *   A 60-second "Staff-First" buffer where station staff receive alerts on their devices before changes appear on public displays or passenger apps.
    *   Integration between platform changes and local station status logs (e.g., broken lift logs) to trigger automated high-priority "Mobility Alerts" for staff dispatch.
    *   Automated, synchronized public address (PA) announcements in Sinhala, Tamil, and English.
    *   Station display screens to immediately fail-safe to "Refer to Station Staff" during network outages rather than displaying stale or incorrect platform information.

*   **Railway Controller:**
    *   Central control entry as the single, non-negotiable source of truth with zero local veto power or overrides from stations.
    *   Immediate, simultaneous updates across the driver’s app and digital station displays without manual communication lag (eliminating phone calls and physical board updates).
    *   Protection of network flow, slot management, and train dwell times to prevent cascading delays across the Main Line (Colombo Fort to Gampaha).
    *   The ability to execute last-minute platform changes within 5 minutes when trains are at the home signal due to signal failures or commuter train breakdowns.
    *   A fallback mechanism: if staff do not acknowledge a high-priority "Staff-First" alert within 15 seconds, the system must release the update to the public immediately.
    *   Immediate automated PA announcements in Sinhala, Tamil, and English triggered the moment "Confirm" is pressed.

---

### Pain Points

*   **Crowd Safety & Physical Infrastructure:** Maradana relies on overhead footbridges; broken lifts make moving passengers with limited mobility slow and difficult. Sudden platform changes during peak rush hours (e.g., Friday 18:00 to Gampaha or Panadura) cause literal stampedes on stairs.
*   **Manual Communication Lag:** Currently, the Controller must telephone the station, station staff inform a porter, and the porter manually updates a board. This delay leads to stampedes.
*   **Information Asymmetry:** Commuters currently see updates on their phones before station staff are notified, causing crowds to run before staff can position themselves.
*   **Accountability Divide:** Station floor staff directly confront angry crowds during failures, while the Railway Controller carries operational safety penalties and delay responsibilities.
*   **Network Bottlenecks:** Holding a train outside Maradana blocks the throat track, cascading delays throughout the entire Main Line within minutes.

---

### Failure Scenarios

*   **Staircase Stampede:** A 5-minute last-minute platform switch during peak commuter rush causes passengers to rush the footbridges, leading to stampedes, injuries, and safety incidents.
*   **Stale Display Data:** A network drop leaves digital screens showing outdated platform numbers, sending crowds in the wrong direction.
*   **Network-Wide Gridlock:** A station rejecting a platform switch forces a train to hold outside the station at the throat, cascading delays across the entire line from Colombo Fort to Gampaha.
*   **Stranded Mobility Passengers:** A train is switched to a platform with a known broken lift without sufficient notice, leaving wheelchair users or elderly passengers unable to traverse stairs in time.
*   **Cascading Delay from Unacknowledged Alert:** Public notification is held back for station staff who fail to respond, turning a 5-minute delay into a 20-minute network failure.

---

### Requirements Wanted (with Priority)

*   **High Priority / Critical:**
    *   *Single Source of Truth / Instant Broadcast:* SmartRail must instantly update the driver's app and station digital displays simultaneously upon central controller confirmation, removing manual phone calls (Railway Controller requirement).
    *   *Display Fail-Safe:* In the event of a network failure, station digital displays must immediately display "Refer to Station Staff" instead of stale data (Station Manager & Railway Controller requirement).
    *   *Staff-First Notification Buffer:* System must send a high-priority notification (audio and vibration) to station staff devices 60 seconds prior to public display and app updates (Station Manager requirement; conditionally accepted by Railway Controller).
    *   *Staff Acknowledgment Timeout:* If station staff do not acknowledge the Staff-First alert within 15 seconds, the system must automatically release the update to public apps and displays (Railway Controller requirement).
    *   *Automated Trilingual PA Announcements:* Automated, synchronized audio announcements must play in Sinhala, Tamil, and English over the station PA system immediately upon platform change confirmation (Station Manager & Railway Controller requirement).
    *   *Automated Mobility Alerts:* The system must cross-reference platform changes with local status logs (e.g., out-of-order lifts) and automatically trigger a high-priority "Mobility Alert" to station staff when an impacted platform is selected (Station Manager requirement).

*   **Disputed / Unaligned Priority:**
    *   *Local Rejection / Veto Power:* Ability for the Station Manager to reject platform changes initiated with less than 15–20 minutes of lead time (Demanded by Station Manager; completely rejected by Railway Controller).

---

### Conflicts Between Roles

1.  **Local Veto vs. Central Absolute Authority:**
    *   *Station Manager:* Demands local authority to reject platform changes if there is less than 15–20 minutes lead time to protect passenger safety and manage crowd flow.
    *   *Railway Controller:* Refuses any local veto, stating central entry must be non-negotiable because holding a train blocks the track throat and causes cascading delays across the entire Main Line.
2.  **Required Lead Time vs. Operational Constraints:**
    *   *Station Manager:* Requires 15 to 20 minutes minimum notice to safely move crowds and passengers with reduced mobility across footbridges.
    *   *Railway Controller:* States 15 minutes is impossible during signal failures or breakdowns, where changes frequently must occur with only 5 minutes' notice when the train reaches the home signal.
3.  **Public Information Timing vs. Staff Buffer:**
    *   *Station Manager:* Insists on a 60-second window where staff are informed before the public (displays and passenger apps) to allow staff to reach stairwells and manage crowd flow.
    *   *Railway Controller:* Views withholding public information as a direct risk to train dwell times; accepts the 60-second buffer *only* if PA announcements fire immediately on confirmation and staff acknowledge receipt within 15 seconds, otherwise triggering an immediate public broadcast.

---

### Open Questions / Assumptions

*   **Open Questions:**
    *   How will the contradiction be resolved between the Railway Controller demanding an immediate automated PA announcement upon hitting "Confirm" and the Station Manager demanding a 60-second blackout before the public is alerted?
    *   What exact mechanism or criteria will determine final authority on platform changes given the Controller's refusal to grant the Station Manager veto power?
    *   What hardware/network infrastructure will be provided to ensure staff devices receive audio/vibratory alerts reliably if station networks drop?
    *   If a lift is logged as broken and a 5-minute platform change is forced, what procedure ensures mobility-impaired passengers reach the train if staff cannot transport them down stairs in time?
*   **Assumptions (from transcript):**
    *   SmartRail encompasses a central controller interface, a driver's app, station digital displays, staff mobile devices, passenger apps, and automated PA systems.
    *   Station staff are equipped with connected personal devices capable of audio, vibration, and interactive acknowledgment.

## 2026-10-04T10:36:50.611Z - How do delays, faults and journey status get reported, and who may change them?

Roles: driver, railway-controller
Transcript: transcripts/driver+railway-controller-2026-10-04T10-36-50-608Z.md

This document summarizes the operational requirements and feedback provided by the driver and railway controller regarding the proposed "SmartRail" system.

### Needs
*   **Driver:** A simple, non-visual interface (single button) to alert the controller of an incident.
*   **Driver:** An audible confirmation (distinct tone/chirp) that the report was received by the controller.
*   **Controller:** A unified dashboard to view driver alerts, telemetry, and manage public-facing status updates.
*   **Controller:** A "Pending Verification" or "Under Investigation" status state.
*   **Controller:** A "Manual Override" function to kill automated data feeds.
*   **System:** A "Queue and Forward" mechanism for reports made in signal blackspots.

### Pain points
*   **Safety:** Current reliance on manual processes and potential distraction from screens/devices while driving.
*   **Information Asymmetry:** Passengers often know about delays via apps before the driver or controller has confirmed them.
*   **Data Reliability:** Telemetry is frequently based on stale data or guesses; automated system updates often conflict with reality.
*   **Workload:** Current process requires the controller to manually cross-reference radio calls with multiple systems/station masters.
*   **Accountability:** The controller is held responsible for inaccurate public information despite having limited control over automated feeds.

### Failure scenarios
*   **Signal Blackspots:** Inability to transmit incident reports when the train is in a tunnel or remote area.
*   **Conflicting Data:** System telemetry (e.g., GPS/track circuits) contradicts the driver’s direct observation of a fault.
*   **Unverified Data:** Automated systems pushing "raw" or "optimistic" ETAs to passengers before a human has verified the situation.
*   **System Guesswork:** The system displaying an estimated arrival time when the train is actually stationary due to an unreported fault.

### Requirements wanted (with priority)
1.  **Manual Gatekeeping (High):** All passenger-facing updates must require human verification by the controller.
2.  **Visual-Free Driver Interface (High):** No screens for the driver; use a single "Incident" button and radio-based audible tones.
3.  **Queueing Mechanism (High):** Ability to store and forward incident reports automatically when returning to network coverage.
4.  **Data Hierarchy (High):** Driver reports must take precedence over system telemetry in the dashboard.
5.  **Status Transparency (Medium):** Dashboard must clearly distinguish between "System Telemetry" (estimates) and "Verified Incidents."
6.  **"Information Unavailable" State (Medium):** System should show "Unavailable" rather than providing an "optimistic" ETA when data is missing.

### Conflicts between roles
*   **Responsibility vs. Automation:** The controller demands total control over what the public sees, rejecting the "algorithm" as a source of truth.
*   **Communication Style:** The driver prioritizes voice/radio and rejects digital notifications, while the controller requires digital system integration to manage information flow.
*   **Accountability:** Both parties agree the current system is a liability, but the controller specifically requires the "gatekeeper" role to mitigate the risk of being blamed for inaccurate automated updates.

### Open questions / assumptions
*   **Assumption:** The "brief" mentioned by both parties is currently inadequate as it lacks a defined verification workflow and system priority logic.
*   **Question:** How will the "Manual Override" interact with the passenger apps to ensure that "Information Unavailable" is displayed correctly when the controller kills an automated feed?
*   **Question:** What is the technical feasibility of distinguishing between "time of occurrence" and "time of arrival" for queued reports in the dashboard?
*   **Question:** If the controller is the sole gatekeeper, how will the system manage communication during high-volume disruption events where the controller may be overwhelmed?

## 2026-10-04T10:37:10.297Z - How does disruption information reach customer service, and what wording and promises are allowed?

Roles: railway-controller, customer-service
Transcript: transcripts/railway-controller+customer-service-2026-10-04T10-37-10-296Z.md

This business analysis is based on the provided transcript between the Railway-Controller and Customer Service.

### Needs
*   **Single Source of Truth:** A centralized, locked-down status field that pushes updates to all passenger-facing channels (boards/apps) simultaneously.
*   **Audit Trail:** A system-wide log capturing user IDs, timestamps, and "before/after" states for every change made.
*   **Feedback Loop:** A mechanism for Customer Service to flag discrepancies between displayed data and physical reality.
*   **Standardized Communication:** A structured way to provide context for delays without allowing subjective or unverified input.
*   **Hierarchy of Status:** A system that prioritizes safety-critical events over standard technical delays.

### Pain points
*   **Manual Communication:** Reliance on radio/phone calls to transmit technical status to passenger-facing systems.
*   **Data Latency:** Passenger boards and apps showing "On Time" while trains are physically stranded.
*   **Inflexible Categorization:** Current inability to handle multi-layered disruptions (e.g., medical emergencies during a signal failure).
*   **Operational Silos:** Lack of a handshake between the control desk and passenger-facing systems.

### Failure scenarios
*   **Misinformation:** Customer service agents being forced to display "On Time" status for trains that are clearly not moving.
*   **Safety/Credibility Disaster:** Unauthorized overrides by customer service leading to incorrect safety-critical status updates.
*   **"Creative Writing" Risk:** Unregulated notes fields leading to unprofessional or panicked explanations being broadcast to passengers.
*   **Communication Gaps:** Passengers receiving contradictory information via push notifications vs. physical station boards.

### Requirements wanted (with priority)
1.  **High: Single Locked-Down Status Field:** Controlled by the Controller; pushes to all boards/apps instantly.
2.  **High: Audit Trail:** Mandatory tracking of all status changes, timestamps, and agent IDs.
3.  **High: Standardized Delay Codes:** Pre-approved menu (e.g., "Signal Failure," "Crew Delay") to ensure professional, accurate messaging.
4.  **Medium: Discrepancy Flag:** A mechanism for CS to alert the Controller of physical discrepancies without altering the source data.
5.  **Medium: Hierarchy/Multi-Cause Logic:** Ability to layer or prioritize status codes (e.g., Medical Emergency overriding Technical Fault).
6.  **Low: Automated Push Notifications:** Integration to ensure status changes trigger real-time alerts to passengers who have booked/scanned in.

### Conflicts between roles
*   **Control vs. Agency:** The Controller demands sole ownership of the "truth" to ensure safety, while Customer Service requires the ability to flag discrepancies to avoid being the "face" of inaccurate information.
*   **Accuracy vs. Speed:** The Controller requires a verification window for accuracy, whereas Customer Service needs immediate updates to manage passenger anger in real-time.
*   **Standardization vs. Reality:** The Controller demands rigid, pre-approved codes to prevent "creative writing," while Customer Service argues these codes are insufficient for complex, multi-layered real-world incidents.

### Open questions / assumptions
*   **Ownership Logic:** If a discrepancy is flagged, what is the defined "verification window" before the Controller must respond?
*   **Compensation Policy:** How are refunds processed? (The current brief is silent on whether CS agents are empowered to process these or if they require paper forms).
*   **Automation:** Is there a technical path to automate the trigger from the track circuit/signal system, or will this remain a manual entry by the Controller?
*   **Communication Channels:** Does updating the central status field automatically trigger push notifications, or are those managed by a separate, disconnected system?

## 2026-10-04T10:42:14.794Z - How accurate must live delay and location information be before passengers can trust it?

Roles: daily-commuter, railway-controller
Transcript: transcripts/daily-commuter+railway-controller-2026-10-04T10-42-14-792Z.md

Here is the structured summary based strictly on the provided transcript:

### Needs
*   **Daily Commuter:**
    *   Accurate arrival time predictions within a 3 to 5-minute threshold.
    *   Alerts about delays received at home *before* leaving for the station.
    *   Immediate indication of delays or unconfirmed statuses (e.g., "Delay Likely") when a train stops, rather than showing a false "On Time" status.
*   **Railway Controller:**
    *   Direct, automated telemetry from trains to track locations without relying on manual entry, phone chains, or driver/controller manual logging.
    *   Control room to remain the single source of truth for public updates.
    *   Automated stationary train flags sent to the control board *first* before reaching the public.
    *   A fast, one-click override mechanism to suppress false alarms, verify track blocks, and publish verified updates.

### Pain Points
*   **Daily Commuter:**
    *   App displaying "On Time" or "Arriving in 5 minutes" when the train is stuck (e.g., near Kelaniya) or already pulling out, destroying daily trust in the system.
    *   False updates ruining daily routines, causing missed trains, delayed work deadlines, and disrupted evening school pickups.
    *   Current official reporting lag forces reliance on crowdsourced updates (such as RDMNS).
*   **Railway Controller:**
    *   Manual phone chain process (driver $\rightarrow$ station master $\rightarrow$ Maradana control $\rightarrow$ controller) takes 5 to 10 minutes minimum to report an incident.
    *   No automatic sensors currently on the board to ping when an engine drops power or holds at a signal on the Main Line.
    *   Inability for controllers (managing multiple incidents) or drivers (operating locomotives) to manually log live updates without data becoming stale.
    *   Risk of public panic and crowd control/safety hazards at stations (e.g., Colombo Fort) if unverified delay alerts trigger rush movements or office swarming.

### Failure Scenarios
*   **Unnoticed Delays:** A train drops power or stops near Kelaniya for 15 minutes while the app continues to display "On Time." The commuter leaves home, arrives at the station, misses the train, fails to meet work deadlines, and misses their evening school pickup.
*   **False Alarm Delays:** A train holds briefly (e.g., 2 minutes) at an outer signal outside Maradana to clear a junction. The app automatically pushes a "Delay Likely" alert to passengers. Commuters panic, swarm station offices, or abandon platforms in Gampaha for alternative trains, leading to crowd hazards at Colombo Fort and trains running with empty carriages.

### Requirements Wanted (with Priority)
*   **Direct Automated Train Telemetry (High Priority):** Automated tracking directly from the train to eliminate reliance on manual phone chains and manual data entry. (Essential per Controller).
*   **Controller Override & Review Authority (High Priority):** A fast, one-click mechanism for controllers to suppress false telemetry alerts, verify incidents, and approve public status updates. (Essential per Controller).
*   **Early Warning Delay Alerts (High Priority):** Pushing immediate unverified/likely delay warnings to commuters before they leave home if a train stops moving. (High Priority per Commuter).
*   **3-to-5 Minute Arrival Accuracy (High Priority):** Keeping app prediction discrepancies within 3–5 minutes of actual arrival times to maintain user trust. (High Priority per Commuter).
*   **Control-First Telemetry Alerts (High Priority):** Automated system flags for stopped trains must route directly to the controller board prior to any public broadcast. (High Priority per Controller).

### Conflicts Between Roles
*   **Immediate Unverified Alerts vs. Verified Source of Truth:**
    *   *Commuter* wants immediate public alerts (e.g., "Delay Likely" or "Status Unconfirmed") the moment a train stops moving, prioritizing early warning over waiting for official verification.
    *   *Controller* rejects broadcasting unverified status updates directly to the public due to operational risks (panic, crowd hazards, empty trains) and insists Control must verify information first as the single source of truth.
*   **Feasibility of Time Precision:**
    *   *Commuter* expects app arrival data accurate to within 3 to 5 minutes.
    *   *Controller* states that expecting 3-to-5-minute accuracy is unrealistic under current operations due to the 5-to-10-minute reporting lag inherent in the manual phone chain process.

### Open Questions / Assumptions
*   **Data Source:** Where is SmartRail's "where the train is now" and "live progress" data actually sourced from in the current system design?
*   **Infrastructure Availability:** Does Sri Lanka Railways currently have the automated train telemetry hardware required to bypass manual logs and provide real-time updates?

## 2026-10-04T10:46:25.359Z - What train timing data does catering need, and what happens when a train is delayed or cancelled?

Roles: catering-provider, railway-controller
Transcript: transcripts/catering-provider+railway-controller-2026-10-04T10-46-25-357Z.md

Here is the analysis based strictly on the provided transcript.

### Needs
*   **Catering-Provider:**
    *   An automated, reliable signal for train delays and cancellations (no manual phone calls).
    *   Guaranteed event sequence and/or a way to query the single authoritative status of a journey before finalizing food prep.
    *   Updates delivered within 15 minutes of occurrence to align with time-sensitive kitchen workflows.
    *   Clear rules and cut-off times regarding train cancellation liability and refunds.
    *   An immutable record/audit trail of when delay or cancellation events were triggered to settle financial liability.
*   **Railway-Controller:**
    *   Real-time position updates derived directly from actual operational data (train GPS or track-circuit occupancy), not manual entries or guesses.
    *   A system that prioritizes network safety and train movement over catering inventory concerns.
    *   Central Operations system to act as the primary digital authority rather than passenger apps or crowdsourced data.

### Pain Points
*   **Catering-Provider:**
    *   Staff work on platforms/galleys and cannot constantly monitor terminals.
    *   Webhooks arriving late or out of sequence cause operational chaos and wasted inventory.
    *   Uncertainty around financial liability for prepped food when trains are cancelled close to arrival time.
*   **Railway-Controller:**
    *   Current operational data is highly fragmented across physical log sheets, radio calls, and phone communications between stations (Fort, Maradana, Kandy, Galle, etc.).
    *   Lack of a dedicated digital "signaling system" for catering; operational reality is inherently messy.
    *   Coverage blind spots, such as express trains losing mobile signals in tunnels (e.g., between Rambukkana and Kadugannawa).
    *   Risk of manual updates being forgotten or delay of operational staff by catering demands.

### Failure Scenarios
*   **Out-of-Order Webhook Delivery:** Receiving a "Delayed" message *after* a "Cancelled" message, causing the kitchen to double-process or incorrectly prep food.
*   **Stale Data Delivery:** A delay notification arriving more than 15 minutes late (e.g., 20 minutes after scheduled arrival), resulting in staff distributing or binning food unnecessarily.
*   **Late Cancellations:** A train being cancelled 10 minutes prior to arrival after meals have already been prepped and bagged, causing total loss of the inventory.
*   **Tunnel Signal Loss:** Trains losing GPS/mobile connectivity in tunnels, leaving the operational status stuck on "last verified position" and providing delayed updates to downstream systems.
*   **Manual Data Omission:** Staff forgetting to manually enter train positions, leading to a system that provides false or "guessed" updates.

### Requirements Wanted (with Priority)

*   **High Priority / Essential:**
    *   **Automated Status Signals:** Automated notifications for delays and cancellations (Catering-Provider).
    *   **Idempotency Keys:** Inclusion of idempotency keys on every status update to prevent double-processing of events (Catering-Provider).
    *   **Pull-based Query Endpoint:** A queryable API endpoint to fetch the authoritative, live journey status on demand before finalizing orders (Catering-Provider / Railway-Controller).
    *   **Automated Data Sources:** Train status generated via GPS or track-circuit occupancy rather than manual input (Railway-Controller).
    *   **Immutable Audit Trail:** Logged updates backed by Central Operations detailing exact timestamps and authorizers for state changes (Railway-Controller / Catering-Provider).

*   **Medium Priority / Unspecified Priority:**
    *   **Defined Cancellation Cut-offs:** Clear rules establishing cut-off times for cancellations relative to train arrival (Catering-Provider).
    *   **Order Cancellation Confirmation Mechanism:** Technical functionality within the system to confirm a catering order was officially cancelled due to railway actions (Catering-Provider).

### Conflicts Between Roles
*   **System Priorities (Operations vs. Catering):** The Railway-Controller maintains that train safety and running the network come first, refusing to let catering demands slow down controllers. The Catering-Provider demands guaranteed, time-sensitive system outputs to protect their kitchen workflows.
*   **Data Guarantees vs. Operational Reality:** The Catering-Provider demands strict guarantees on event sequence and data freshness (<15 min latency). The Railway-Controller insists that real-world operations cannot guarantee strict sequence or continuous signal connectivity (e.g., tunnels).
*   **Financial Liability:** The Catering-Provider insists the railway must pay for wasted food if a train is delayed/cancelled after prep. The Railway-Controller explicitly states they do not manage food inventory and focus strictly on operational movement.

### Open Questions / Assumptions
*   **Financial Liability:** Who pays for prepped/bagged food when a train is cancelled close to arrival time?
*   **Passenger Refund Liability:** Who bears the loss/refund liability if a train is delayed and the passenger leaves the platform?
*   **Technical Authority:** What exact system acts as the single technical source of truth during operational discrepancies?
*   **Cancellation Confirmation:** Does the system currently support a specific workflow for catering providers to confirm an order was cancelled by the railway?
*   **Signal Gap Handling:** How will the system present status to catering when a train is between track circuits or out of cell coverage in tunnels?

## 2026-10-04T10:50:54.809Z - How do we test live train status, delays and disruptions without real trains?

Roles: qa-engineer, railway-controller
Transcript: transcripts/qa-engineer+railway-controller-2026-10-04T10-50-54-807Z.md

### Needs
* Robust mocks and failure injection frameworks for automated test runs to simulate edge cases without relying on live physical telemetry.
* Measurable threshold for stale telemetry (specifically, if telemetry is stale by more than 120 seconds).
* Explicit, manual override switch on the railway controller's control desk to lock the route and declare a verified state.
* Ability for the railway controller to act as the single source of truth when telemetry gives rubbish or goes dark.
* Automated test harness simulation of the railway controller's operations web application using deterministic test double workflows and mock authorization gates.
* System behavior during a disruption to hold the previous safe state and display a clear warning that operations are under manual review until explicit sign-off occurs.
* Design of test harnesses to wait for explicit railway controller approval tokens rather than using rigid countdown timers.

### Pain points
* Relying on live physical telemetry from the Main or Coastal Line during a test run is a recipe for flaky pipelines.
* Faulty telemetry or automated test harness injecting garbage data creates operational chaos between Colombo Fort and Badulla.
* The brief completely misses the crucial workflow for how a controller audits, freezes, and forcefully overrides bad event streams before they hit public boards or passenger apps.
* Trying to force an arbitrary operational timeout SLA for controller sign-offs (whether 30 seconds or 5 minutes) when lives and rolling stock are moving.

### Failure scenarios
* Total signal loss from physical telemetry feeds (e.g., complete blackout of GPS or block-signaling feeds from a train running near Gampaha).
* UI freezing indefinitely, showing a misleading "on-time" status, or failing to clearly indicate stale data with a timestamp when telemetry stops updating.
* Sudden track switching or severe monsoon weather delays causing cascading cancellations, injected via out-of-order, duplicate, and conflicting events directly into the event stream.
* Passengers running across platforms at Maradana or catering orders scrambled because a sensor hiccuped and triggered automatic passenger alerts or catering reroutes without sign-off.
* Controller dashboard hanging or approval timing out past defined thresholds.
* System automatically broadcasting an unverified disruption or escalating blind if subjected to a rigid timeout.
* The system auto-publishing panic instead of holding the previous safe state.

### Requirements wanted (with priority)
1. **Critical:** Implement a manual override switch on the operations control desk to lock routes, declare a verified state, quarantine faulty telemetry feeds, and push manual corridor locks. *(Priority: High)*
2. **Critical:** Automate the end-to-end flow for live journey updates (abruptly delayed train propagating instantly from operations to passenger mobile push notifications and platform LED boards). *(Priority: High)*
3. **Critical:** Automate the end-to-end flow for a food order placed by a commuter on the Coast Line where the train gets unexpectedly rerouted, triggering automatic recalculation of the collection station. *(Priority: High)*
4. **High:** Flag telemetry as stale and prevent system usage if it is stale by more than 120 seconds. *(Priority: High)*
5. **High:** Ensure automated passenger alerts and catering reroutes wait for explicit manual sign-off from the railway controller during a disruption. *(Priority: High)*
6. **High:** Ensure that until explicit manual sign-off/override occurs, the system's *only* allowed behavior is to hold the previous safe state and display a clear warning that operations are under manual review. *(Priority: High)*
7. **Medium:** Implement deterministic test double workflows and mock authorization gates in the automated test harness to simulate the controller's manual sign-off via programmatic sign-off tokens or API approval payloads. *(Priority: Medium)*

### Conflicts between roles
* **QA Engineer vs. Railway Controller (Timeout SLAs / Automation vs. Safety):** The QA engineer needs a defined, measurable threshold or timeout SLA (e.g., a 30-second timeout) for the controller's sign-off to prevent pipelines and tests from hanging indefinitely. Conversely, the railway controller strongly rejects any rigid countdown timer or arbitrary timeout, stating that safety overrides speed, incident durations vary wildly (from 30 seconds to 10 minutes), and the system must never auto-publish or forcibly escalate based on an automatic timer.

### Open questions / assumptions
* **Question (QA Engineer):** What is the exact timeout SLA for the controller's sign-off before a critical disruption alert is forcibly escalated or dropped, given that without a defined number, testing unverified disruption safety is a guess?
* **Assumption (QA Engineer):** Assumed that testing human-in-the-loop gates requires programmatic mocks (like injecting a pending disruption event and posting a sign-off token after a controlled delay, such as 5 seconds, or failing if approval times out past 30 seconds) to verify that public boards and passenger apps remain locked in a safe state rather than auto-publishing garbage.
* **Assumption (Railway Controller):** Assumed that there is no fixed countdown clock for safety, and the system must indefinitely hold the previous safe state with a manual review warning until the human controller explicitly intervenes, regardless of automated pipeline or testing needs.

## 2026-10-04T10:53:32.311Z - A train is cancelled at rush hour: walk through what each of you does and needs, minute by minute.

Roles: railway-controller, driver, station-manager, customer-service, daily-commuter
Transcript: transcripts/railway-controller+driver+station-manager+customer-service+daily-commuter-2026-10-04T10-53-32-309Z.md

This report summarizes the operational insights provided by stakeholders regarding the current railway disruption management process.

### Needs
*   **Centralized Control:** A single-action "Cancellation" trigger that updates all downstream systems (screens, apps, driver terminals) simultaneously.
*   **Proactive Communication:** A "Pre-Warning" or "Standby" status to allow staff to prepare before public-facing announcements are made.
*   **Contextual Data:** Customer service needs to know the *reason* for cancellations to provide accurate guidance.
*   **Offline Resilience:** Systems must function in tunnels/dead zones and provide "last known" status.
*   **Hierarchy of Truth:** A clear definition of which system/role has authority over platform and signaling data.
*   **Personalized Alerts:** Passengers require automated push notifications for their specific commute.

### Pain points
*   **Manual Relaying:** Controller must manually notify station masters, causing lag.
*   **Information Silos:** Inconsistent data across platform boards, mobile apps, and internal terminals.
*   **Reactive Management:** The current system does not allow for advance crowd control or staff preparation.
*   **Safety/Distraction:** Drivers are overwhelmed by unnecessary passenger-facing information.
*   **Lack of Visibility:** Customer service lacks data on train contents (e.g., accessibility needs) and refund/compensation protocols.
*   **Stale Data:** Public displays showing "On Time" information when the network is actually failing.

### Failure scenarios
*   **Information/Safety Conflict:** App indicates "Go" while physical signal indicates "Stop."
*   **Data Lag:** Passengers receiving cancellation notices at the same time as station staff, leading to immediate crowd surges.
*   **Dead Zone Disconnect:** Drivers losing instructions while in tunnels or signal-dense areas.
*   **System Trust:** Boards displaying outdated/incorrect info, causing staff to face hostility from passengers.

### Requirements wanted (with priority)
1.  **High (Safety/Ops):** "Single-button" high-priority notification for drivers (must work offline, non-distracting).
2.  **High (Operational):** "Pre-Warning" status for Station Managers (5-minute lead time before public notification).
3.  **High (Operational):** Local authority for Station Managers to override displays for emergency crowd control.
4.  **Medium (System Integrity):** "Sync Status" indicator on displays to warn if data is stale (>2 minutes).
5.  **Medium (Customer Exp):** Automated push notifications to passengers based on season ticket/route data.
6.  **Medium (Customer Exp):** Inclusion of alternative transport options and "Refund Eligibility" status in all cancellation notices.

### Conflicts between roles
*   **Controller vs. Station Manager:** Controller prioritizes track clearance; Station Manager requires "Pre-Warning" time, which may conflict with the Controller's need for immediate action.
*   **Station Manager vs. Controller:** Station Manager requires the autonomy to change platform data during emergencies, while the Controller requires a "Hierarchy of Truth" to prevent chaos.
*   **Driver vs. System:** Drivers reject any "passenger-facing" data on their terminals, viewing it as a distraction from safety-critical signaling.

### Open questions / assumptions
*   **Authority:** Who holds the final "source of truth" when a station manager's emergency notice conflicts with the Controller’s network-wide plan?
*   **Liability:** How will the system resolve the discrepancy between digital "Go" instructions and physical "Stop" signals?
*   **Operational Scripting:** Who is responsible for defining the "approved scripts" for compensation and alternative routes to ensure consistency across the customer service team?
*   **Accessibility Visibility:** How can the system track "high-need" passengers without infringing on privacy or creating additional manual work for the driver?

## 2026-10-06T04:48:11.063Z - Walk through a full shift: how you monitor and control the network today (trains, delays, holds, platforms, incidents, communication with drivers and stations), and what you need from the operations web application

Roles: railway-controller
Transcript: transcripts/railway-controller-2026-10-06T04-48-11-061Z.md

### Needs
*   **Operational Override:** Ability to push status updates that immediately override schedules across all platforms and the passenger app.
*   **Mandatory Acknowledgment Loop:** Digital handshake requirement for all overrides; messages must remain "Pending" until acknowledged.
*   **System-Enforced Fail-safe:** Automatic transition to a generic "Service Disrupted" state if an override is not acknowledged within 60 seconds.
*   **Centralized Authority:** The system must act as the "sole authority," with digital displays tethered to the controller's command.
*   **Conflict Resolution Mechanism:** A system for station masters to flag local observations that conflict with central commands.
*   **Audit and Accountability:** Tracking of who initiated an override, why, and at what priority level.
*   **Dual-Key Authorization (Operational Interlock):** Mandatory two-party sign-off (Station Master and Controller) to resume service after a safety-critical stop.

### Pain Points
*   **"Static" System:** Current system is built for perfect schedules and fails during delays or track obstructions.
*   **Stale Data:** GPS tracking is often missing or inaccurate, forcing the controller to verify train positions manually.
*   **Information Asymmetry:** Discrepancies between what the controller knows, what the station staff announces, and what the app displays lead to passenger chaos.
*   **Lack of Accountability:** Current reliance on verbal "read-backs" without a digital audit trail.
*   **Manual Coordination:** Over-reliance on radios and landlines to manage disruptions.

### Failure Scenarios
*   **"Phantom" Information:** The system displays "On Time" for a train that is stalled or terminated, creating false expectations and potential public unrest.
*   **Unacknowledged Instructions:** A notification is sent to a busy station master, but it goes unseen, leading the controller to assume the message was received when it wasn't.
*   **Contradictory Information:** A station master makes a PA announcement that conflicts with the central digital display/app.
*   **"Rogue" Local Overrides:** A station master overrides central command without a clear reason, causing system-wide confusion.
*   **Premature Resumption:** Service is resumed on a track that has not been verified as clear by ground staff after an emergency.

### Requirements Wanted (Priority)
1.  **High: Operational Override & Mandatory Acknowledgment:** System must force a digital handshake for all overrides.
2.  **High: Fail-safe "Service Disrupted" State:** Automatic reversion to a neutral state if the acknowledgment loop fails within 60 seconds.
3.  **High: Dual-Key Authorization (Interlock):** Mandatory station master and controller sign-off to resume service after a safety-critical stop.
4.  **Medium: Local Station Override Policy & Metadata:** Ability for station masters to override for safety, provided they supply a Priority Level, Reason Code, and User ID.
5.  **Medium: Conflict Alarm:** Real-time alerting on the controller’s console when a local override contradicts a central command.

### Conflicts between Roles
*   **Centralized vs. Local Authority:** The Controller views Central Operations as the sole source of truth, while station masters may feel the need to act on local observations. The controller demands the system "tether" local displays to central commands to prevent "rogue" announcements.

### Open Questions / Assumptions
*   **Assumption:** The controller assumes that a "digital handshake" is a sufficient proxy for a human actually understanding and acting upon an instruction.
*   **Open Question:** How will the system handle the technical implementation of "locking" the public displays to central authority while still allowing for local safety-critical overrides?
*   **Open Question:** Does the current infrastructure support the "dual-key" software interlock required to physically prevent signal changes without both parties' digital signatures?

## 2026-10-06T05:07:06.320Z - Describe your normal shift and your normal week in the control room right now: hour by hour what you monitor and decide, which logs, radios, phones or screens you use, who you talk to, what goes wrong and how often.

Roles: railway-controller
Transcript: transcripts/railway-controller-2026-10-06T05-07-06-319Z.md

### Needs
*   **Dissemination Override:** A single-click mechanism to update public displays (platforms, apps, catering) and internal interfaces simultaneously when telemetry conflicts with manual reports.
*   **Role-Based Dissemination:** A system that presents different status labels for the same incident based on the user's role (e.g., "Delayed" for passengers vs. "Track Occupied/Hazard" for maintenance).
*   **Acknowledgment Loops:** Mandatory "Read Receipts" for safety-critical alerts, preventing staff from performing other tasks until they acknowledge the hazard.
*   **Fail-Safe Visibility:** A "Disconnected/Unknown" visual state for terminals when network connectivity is lost.
*   **Communication Integrity Monitoring:** A "heartbeat" monitor for all terminals to alert the controller if any device goes offline.

### Pain Points
*   **Data Inaccuracy:** The current brief assumes telemetry is always correct, forcing the controller to manually manage discrepancies.
*   **Communication Overhead:** Manual radio calls are slow, prone to human error, and require constant attention.
*   **Lack of Architectural Clarity:** The current brief fails to distinguish between the "Information Layer" and the "Control Layer," risking dangerous automated interference with safety interlocks.
*   **Inadequate Safety Categorization:** The system currently treats service delays and safety-critical incidents identically.

### Failure Scenarios
*   **Automated Interference:** The system attempts to auto-route a train based on "smart" logic while the controller has manually declared it a mechanical failure.
*   **Maintenance Risk:** Staff misinterpret a "Delayed" status as safe to approach the track, leading to potential accidents.
*   **Unacknowledged Hazards:** Safety-critical alerts are ignored or missed by staff, leaving the controller unaware of the danger.
*   **Network Failure:** A power outage or network drop occurs, and the system fails to notify the controller that messages are not reaching their destination.

### Requirements Wanted
| Requirement | Priority |
| :--- | :--- |
| Dissemination Override (Information Layer only) | Critical |
| Role-Based Dissemination (Audience-specific labels) | Critical |
| Acknowledgment Loop (Active blocker/Read Receipt) | Critical |
| Fail-Safe Visibility (Disconnected/Unknown state) | High |
| Communication Integrity/Heartbeat Monitor | High |

### Conflicts Between Roles
*   **Controller vs. System Logic:** The controller requires the system to remain "dumb" regarding safety interlocks, whereas the proposed "SmartRail" concept implies a monolithic system where data impacts control.
*   **Maintenance/Station Staff vs. Controller:** Currently, there is a lack of automated confirmation, creating a disconnect between the Controller’s safety directive and the staff's awareness/compliance.

### Open Questions / Assumptions
*   **Assumption:** The brief assumes a monolithic, "one-bucket" architecture, which the controller explicitly rejects as a safety risk.
*   **Assumption:** The brief assumes constant connectivity, which the controller identifies as a "dangerously optimistic" oversight.
*   **Open Question:** How will the "Communication Integrity" map be integrated into the existing control console without cluttering the interface?
*   **Open Question:** What is the precise protocol for "manual safety procedures" to be enacted when the system enters a "Disconnected/Unknown" state?

## 2026-10-06T05:13:03.173Z - Describe your normal shift and your normal week in the control room right now: hour by hour what you monitor and decide, which logs, radios, phones or screens you use, who you talk to, what goes wrong and how often.

Roles: railway-controller
Transcript: transcripts/railway-controller-2026-10-06T05-13-03-171Z.md

### Typical day
*   **05:45:** Clock in; scan the logbook; receive handover from outgoing controller (review "hot" list, radio logs, and handwritten notes).
*   **Post-handover:** Take responsibility for the desk; make initial platform allocation decisions for the 06:05 commuter rush.
*   **Ongoing (Shift duration):** Monitor traffic flow, cross-reference the physical track diagram board with radio reports, and manage operational incidents.
*   **14:00:** Hand over the "Daily Occurrence Book" and shift report sheet to the relief controller; walk them through "live" issues; sign off responsibility.

### Typical week
*   **Weekdays:** Focus on throughput and high-volume commuter traffic; managing the morning rush at Colombo Fort.
*   **Weekends:** Focus on long-distance leisure/tourist travel; perform a "Rolling Stock Audit" to verify carriage attachments; manage increased dwell times and irregular travel patterns.

### Tools and information sources used today
*   **Logbook / Daily Occurrence Book:** Primary non-negotiable record of events.
*   **Physical track diagram board:** Wall-mounted board with indicator lights for section occupancy.
*   **Radio logs and radio feed:** Communication with signalmen, drivers, and station masters.
*   **Shift report sheet:** Summary of long-distance train status.
*   **Digital displays:** Used for reference, though noted as often lagging behind physical reality.
*   **Handwritten notes:** Specifically regarding the Coastal Line.

### People they talk to
*   **Outgoing/Relief Controller:** For handover and status updates.
*   **Signalmen:** To verify train positions.
*   **Station Masters:** To coordinate platform capacity, manage crowds, and address delays.
*   **Train Drivers:** For direct updates on train status and mechanical issues.
*   **Guards/Technicians:** Dispatched to address passenger alarms or mechanical faults.

### What goes wrong and how often
*   **Stalled commuter trains (passenger alarms or door obstructions):** Occurs 3–4 times per week. Resolved by stopping following traffic, using the wall diagram to freeze block sections, dispatching staff to investigate, and notifying station masters of cascading delays.
*   **Signal failures:** Not stated (frequency).
*   **Track defects:** Not stated (frequency).
*   **Engine failures:** Not stated (frequency).
*   **Short-formed intercity trains:** Not stated (frequency).
*   **Intermittent signals:** Not stated (frequency).
