# Driver

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

## 2026-10-04T10:48:43.554Z - How do drivers sign in, and what happens if a device is lost or a status is spoofed?

Roles: security-engineer, driver
Transcript: transcripts/security-engineer+driver-2026-10-04T10-48-43-527Z.md

### Needs
*   **Security (Engineering):** Cryptographic device binding; MFA (biometrics/physical cards); strict session invalidation; short-lived tokens; remote revocation/kill-switch; protection against session hijacking and service spoofing.
*   **Operational Continuity (Driver):** Instant access to replacement hardware; offline functionality in remote areas/tunnels; manual override for equipment failure; ability to switch between train sets seamlessly.

### Pain Points
*   **Environmental Obstacles:** Biometrics failing due to grease, glare, or physical conditions.
*   **Equipment Reliability:** Tablet hardware failure in remote locations (tunnels/cuttings).
*   **Connectivity:** Lack of network signal in remote areas preventing authentication or MDM checks.
*   **Administrative Lag:** Delays in IT authorization for device replacement or account unlocking.
*   **Physical Logistics:** Risk of losing or damaging a physical NFC ID card while in transit.

### Failure Scenarios
*   **Stolen Device:** An attacker gains access to a tablet, spoofs train positions, or sends unauthorized "clear to proceed" signals.
*   **Hardware Failure:** A tablet dies mid-journey; the driver cannot log into a spare unit due to binding/provisioning requirements, halting the train.
*   **Authentication Lockout:** The app locks the driver out due to a lack of server connectivity or failed biometric/card read, causing service delays.
*   **Physical Loss:** The driver loses the NFC ID card mid-shift, leaving them unable to operate any terminal.
*   **Systemic Bottleneck:** IT/Operations is unavailable to authorize a "kill switch" or a device swap, causing a prolonged service stoppage.

### Requirements Wanted
| Requirement | Priority |
| :--- | :--- |
| **NFC ID Card + PIN** (Hardware-backed identity) | High |
| **Offline Integrity** (Signing logs locally via TEE) | High |
| **Instant Hardware Swap** (Ability to log into any spare unit) | High |
| **Manual Override** (Logbook-signed bypass for tech failure) | Medium |
| **Panic Button/Kill Switch** (Operations-triggered remote revocation) | Medium |

### Conflicts Between Roles
*   **Security vs. Operations:** The Security Engineer prioritizes strict, cryptographically enforced access, while the Driver prioritizes the "Safety First" principle of moving the train, viewing security protocols as potential service blockers.
*   **Centralization vs. Autonomy:** The Security Engineer wants centralized control over revocation and device identity (MDM), while the Driver demands autonomy to bypass tech failures immediately to prevent service delays.

### Open Questions / Assumptions
*   **Revocation Authority:** Who is authorized to trigger the "kill" command for a stolen device? (Ops vs. IT).
*   **Recovery Protocol:** What is the specific procedure for when an NFC card is lost or a reader fails? 
*   **Accountability:** How will a "manual override" be reconciled with security requirements to prevent abuse or spoofing?
*   **Hardware Standardization:** Can all fleet tablets reliably support NFC and TEE (Trusted Execution Environments) for offline cryptographic signing?

## 2026-10-04T10:51:39.963Z - What does a driver need to know about platforms, stops and station closures before and during a journey?

Roles: driver, station-manager
Transcript: transcripts/driver+station-manager-2026-10-04T10-51-39-962Z.md

This report summarizes the operational requirements and feedback regarding station closure protocols based on the transcript provided.

### Needs
*   **Driver:** A single "source of truth" (Controller) for operational instructions; offline reliability; minimal, non-distracting interfaces; clear authority to skip stops.
*   **Station Manager:** Autonomy to trigger localized platform alerts (e.g., overcrowding, structural failure) without waiting for head-office approval; a "fail-safe" display state when networks drop.
*   **Passengers:** Synchronized, accurate information between platform displays and onboard train announcements to prevent confusion or panic.

### Pain Points
*   **Communication Lag:** Station managers are often the last to know about line-wide closures, leading to passenger frustration and safety risks.
*   **Information Asymmetry:** Discrepancies between physical platform boards (showing "On Time") and reality (trains not arriving).
*   **Interface Distraction:** Drivers are currently overwhelmed by unnecessary screen data while operating trains.
*   **System Fragility:** Reliance on network connectivity; current lack of a "handshake" protocol between station staff and the Controller.

### Failure Scenarios
*   **Silent Failure:** A station manager closes a platform, but the Controller is unaware, causing the train to arrive at an unsafe location.
*   **Ghost/Stale Data:** Screens displaying incorrect information or going blank during signal loss, causing passenger panic.
*   **Operational Overreach:** The potential for local station staff to trigger automated changes that could conflict with the Controller’s safe movement of the train.

### Requirements Wanted
| Requirement | Priority |
| :--- | :--- |
| **Operational Handshake:** A protocol where Station Alerts trigger a Controller dashboard notification before any train instruction is issued. | High |
| **Offline Fail-Safe:** Screens must display "Service Unavailable" or "Last Updated" timestamps rather than blank or stale data during signal loss. | High |
| **Read-Back Confirmation:** A single-button acknowledgment for drivers to confirm receipt of skip-stop orders (only when stationary). | High |
| **Clear Authority:** Operational instructions must only originate from the Controller. | High |
| **Local Alert Autonomy:** Station managers must have the ability to mark platforms as "Closed" locally to manage immediate safety. | High |
| **Simplified Cab Interface:** One-line text instructions only; no graphics or running commentary on the driver's display. | Medium |

### Conflicts Between Roles
*   **Operational vs. Local Authority:** The driver insists that only the Controller should issue operational commands, while the station manager requires the ability to trigger local alerts immediately to prevent overcrowding/crushes.
*   **Communication Responsibility:** The driver refuses to act as a public address system; the station manager agrees this is their role but notes the current lack of a synchronized system to execute it.

### Open Questions / Assumptions
*   **Assumption:** The driver assumes that if the "SmartRail" system loses signal, it will remain silent, but the station manager assumes the system must be robust enough to handle "fail-safe" messaging during such outages.
*   **Question:** What is the specific escalation path if the Controller is too busy to acknowledge a "Station Alert" sent by a station manager?
*   **Assumption:** Both parties assume that an automated "handshake" between the station's local terminal and the Controller's dashboard will resolve the safety gap regarding unexpected closures.

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

## 2026-10-04T10:54:05.321Z - What are the biggest risks across the apps and the integration, and how would each be caught?

Roles: security-engineer, qa-engineer, catering-provider, driver
Transcript: transcripts/security-engineer+qa-engineer+catering-provider+driver-2026-10-04T10-54-05-319Z.md

### Needs
* **Security:** 
  * Proper identity context and robust authorization during API handshakes to prevent Broken Object-Level Authorization (BOLA).
  * Every API request must be authenticated, scoped, and rate-limited.
  * Non-repudiation and signed requests for operational commands (e.g., train position updates).
  * Exclusion of sensitive PII from logs and audit trails.
  * An automated reconnaissance flagging mechanism for spikes in 403 Forbidden errors.
* **Quality Assurance:**
  * Definition of a "safe" system state.
  * A clear definition of the "source of truth" hierarchy to resolve conflicts between GPS, track sensors, and manual driver updates.
  * Defined behavior for passenger displays when data is rejected (e.g., "Delayed," "Unknown," or last known good state) with specific numbers.
* **Catering Provider:**
  * Financial protection for meal preparations made before a cancellation/refund.
  * An idempotent way to reconcile order lists once network synchronization is restored after connectivity loss.
  * A hard cut-off time after which orders cannot be changed or canceled.
  * A local cache of orders at the station terminal that works independently of central server availability.
  * Clear accountability and liability rules for wasted food if synchronization fails.
* **Driver:**
  * Zero digital system distractions while the train is in motion; physical trackside signals and rule books take precedence.
  * Voice communication via radio with the railway controller for operational issues during signal drops.
  * Physical separation or removal of the software/screen from the driver's line of sight when moving.

---

### Pain points
* Developers blindly assuming any token grants access to any record, leading to BOLA vulnerabilities.
* The current brief completely ignoring non-repudiation for operational commands.
* Lack of a defined "source of truth" hierarchy for train state data (GPS vs. track sensors vs. manual updates).
* Risk of financial loss for catering providers due to cancellations occurring during dead zones or network drops.
* Glitchy or screaming digital apps causing dangerous distractions for drivers inside the cab when losing GPS or cellular signal.
* Apps potentially displaying out-of-date timings or data that conflict with physical trackside rules.

---

### Failure scenarios
* A passenger manipulates a request (changing a `passenger_id` or `order_id` in a URL parameter) to view someone else's journey details or catering order.
* Automated reconnaissance attempts via the API going unnoticed without rate-limit and 403-error spike monitoring.
* A spoofed signal sent from a driver's app meant to cause panic (e.g., false train position update).
* The system receiving a spoofed "arrived" signal at one station while actually located elsewhere, leading to unhandled auto-updates or lockouts.
* Malformed data injection (out-of-bounds coordinates, future timestamps, invalid train IDs) crashing the system or blindly updating public boards.
* Receiving an "arrived" update without a corresponding "departed" update for a station, or handling out-of-order sequence updates during high latency.
* A passenger attempting to cancel or refund a catering order while a train is in a dead zone, leading to wasted food and supply cost losses.
* Central server going down, leaving the station terminal without an independent local cache of upcoming orders.
* The app losing GPS or cellular signal in a cutting or tunnel and glitching, screaming for attention, or displaying out-of-date timings while a driver approaches a restricted speed zone.
* A driver attempting to manually override a stuck journey status or type out messages on a phone instead of focusing on physical signals and radioing central operations.

---

### Requirements wanted (with priority)

| Requirement | Priority |
| :--- | :--- |
| Implement authentication, scoping, and rate-limiting on every API request; flag spikes in 403 Forbidden errors. | High (Security) |
| Ensure signed requests (non-repudiation) for operational commands like train position updates. | High (Security) |
| Keep sensitive PII (names, phone numbers) completely out of logs and audit trails. | High (Security) |
| Define the system's "safe" state and establish a clear "source of truth" hierarchy (GPS, sensors, manual updates). | High (QA / Operations) |
| Implement negative, state transition, and latency stress testing for malformed or out-of-order payloads. | High (QA) |
| Provide an idempotent reconciliation mechanism and local order caching for station catering terminals. | High (Catering) |
| Enforce a hard cut-off time for catering order cancellations and define liability/cost allocation rules for dropped-connection losses. | High (Catering / Business) |
| Keep software/screens out of the driver's line of sight and eliminate motion distractions (rely on trackside signals and radio). | High (Safety / Driver) |

---

### Conflicts between roles

* **Catering Provider vs. Passenger App / System Design:** The catering provider demands a hard cut-off after which order changes are impossible to protect their kitchen prep margins, conflicting with a passenger-facing app's expectation of flexible, real-time refunds or cancellations during transit.
* **Security/QA/Engineering vs. Driver:** Technical teams focus on app-based states, digital syncs, and fail-safe displays, whereas the driver explicitly rejects app notifications, digital overrides, and screen dependencies while in motion, insisting that software must not distract from physical signals and radio communication.

---

### Open questions / assumptions

* **Open Questions:**
  * What is the defined "source of truth" hierarchy when GPS, track sensors, and manual driver updates conflict?
  * What exact message or status (e.g., "Delayed," "Unknown," or last known good state) should display on the passenger board when data is rejected?
  * Who is legally and financially liable for wasted catering supplies when network synchronization fails or a cancellation occurs in a dead zone?
* **Assumptions:**
  * It is assumed by developers that holding a token means a user is authorized to view any record (identified as a dangerous security mistake).
  * The catering provider assumes that without local caching and idempotent reconciliation, network drops will result in unrecoverable financial losses.
  * The driver assumes that standard operating procedure relies strictly on physical trackside signals, rules of the book, and voice radio, rendering the app completely secondary in the cab.

## 2026-10-06T05:05:40.588Z - Describe your normal shift and your normal week right now: hour by hour what you do, which instructions, papers, radios or devices you use, who you talk to, what goes wrong and how often.

Roles: driver
Transcript: transcripts/driver-2026-10-06T05-05-40-588Z.md

This document outlines the requirements and operational constraints for the "SmartRail" system based on the driver's perspective.

### Needs
*   **Operational Reliability:** A system that functions offline and handles connectivity drops (tunnels/cuttings) without manual intervention.
*   **Controller Authority:** Digital data must be verified by the controller; the system must respect the chain of command.
*   **Operational Clarity:** Information must be presented in plain, bold text without ambiguous icons or complex legends.
*   **Situational Awareness:** A "Fail-Safe" mode where the display clears or goes dark if data is stale, conflicted, or unreliable.

### Pain Points
*   **Distraction/Workload:** The driver objects to "data entry" tasks, acknowledging pop-ups, or decoding complex visual interfaces while operating the train.
*   **Connectivity Instability:** Frequent signal loss in tunnels/cuttings makes constant-data-connection apps impractical.
*   **Accountability:** Concern regarding who takes responsibility for incorrect data pushed to the cab.

### Failure Scenarios
*   **Conflict with Physical Signals:** The system displays "Clear" while the physical signal is "Red."
*   **Stale Data:** The system displays outdated speed restrictions after re-establishing a connection.
*   **System Frozen:** The device appears to be running but is displaying inaccurate, non-updated information.
*   **Data Mismatch:** The system pushes an instruction that contradicts verbal radio commands.

### Requirements Wanted (Priority)
1.  **High Priority:** **Offline-Capable Background Sync.** Data must update automatically without manual "sync" buttons or reconnecting spinners.
2.  **High Priority:** **Fail-Safe Display.** If the system detects a conflict or loses connection, it must go black or display "Data Unavailable" to prevent the use of stale/wrong info.
3.  **High Priority:** **Controller Verification.** All instructions must be tagged with a unique reference number or "Confirmed by Control" text that matches radio commands.
4.  **Medium Priority:** **Minimalist Interface.** No alerts, alarms, or notifications that require physical acknowledgement (tapping/dismissing) while the train is in motion.
5.  **Low Priority:** **Remote Logging.** Errors and discrepancies must be logged at the control center, not on the driver's console.

### Conflicts Between Roles
*   **System Design vs. Driver Autonomy:** The "SmartRail" design philosophy (notifications, status badges, alerts) directly conflicts with the driver's requirement for a "quiet" cab and absolute reliance on physical track signals.
*   **Accountability Gap:** The driver refuses to accept responsibility for data pushed by the system unless it is explicitly verified by the controller.

### Open Questions / Assumptions
*   **Assumption:** The driver assumes that if the screen is "dark" or "unavailable," it is a sufficient notification of system failure.
*   **Open Question:** If the system goes dark, how will the control center be alerted that the driver is no longer receiving digital updates?
*   **Open Question:** How will the "unique reference number" be generated and cross-referenced by the controller in real-time?
*   **Assumption:** The driver assumes that the controller has a terminal capable of pushing these specific verified instructions alongside radio commands.

## 2026-10-06T05:10:56.297Z - Describe your normal shift and your normal week right now: hour by hour what you do, which instructions, papers, radios or devices you use, who you talk to, what goes wrong and how often.

Roles: driver
Transcript: transcripts/driver-2026-10-06T05-10-56-295Z.md

### Typical day
*   **04:30:** Arrive at Maradana running shed.
*   **Arrival tasks:** Sign into the logbook, collect the "driver’s pack" (paper circulars).
*   **Pre-departure:** Check watch against station clock, grab radio, perform physical walk-around of the locomotive (brake pipe pressure, headlights, sanders).
*   **Departure:** Receive "Right Away" signal from guard or platform staff.
*   **During shift:** Monitor signal aspects, track-side markers, and speed restrictions; operate throttle and brake.
*   **End of shift:** Return locomotive to the shed, fill out the official logbook (arrival time, engine number, mileage, delays), fill out the "Defect Book," hand back radio and paperwork, get foreman sign-off, return key, and clock out.

### Typical week
*   **Weekdays:** Focus on high-frequency commuter services (e.g., Veyangoda or Panadura); high pressure to maintain a tight timetable.
*   **Weekends:** Fewer commuter services, more intercity trains (e.g., to Kandy); trains are often heavier and more crowded; increased frequency of track maintenance and "caution" orders due to track crews working.

### Tools and information sources used today
*   **Logbook:** For recording shift start/end, engine details, and delays.
*   **Driver’s pack:** Paper circulars detailing speed restrictions, track maintenance, and signal failures.
*   **Radio:** For communication with the signalman or controller.
*   **Station clock:** To synchronize time.
*   **Locomotive gauges:** To monitor mechanical status.
*   **Signal aspects:** The primary visual language for navigating the line.
*   **Defect Book:** For reporting mechanical or equipment issues.
*   **Track-side markers:** Used to verify speed restrictions.
*   **Emergency trackside telephone:** Used as a backup for radio communication.

### People they talk to
*   **Shed foreman:** To report equipment issues (e.g., radio problems) and for shift sign-off.
*   **Guard/Platform staff:** To receive the "Right Away" signal.
*   **Signalman/Controller:** To receive movement instructions or authority to pass signals.
*   **Track gangs:** For hand signals/red flags during weekend maintenance.

### What goes wrong and how often
*   **Signal failures / Communication breakdowns:** Occurs at least two or three times a week (increased frequency during monsoons).
    *   **Process:** Bring train to a full stop; contact the signalman via radio; wait for verbal or written clearance ("Caution Order" or "Paper Authority"); record the delay and signal number in the logbook.
*   **Mechanical/Equipment issues:** "Usually" happens (e.g., radio cutting out, flickering headlights, sluggish engine).
    *   **Process:** Note the issue in the "Defect Book" at the end of the shift for the shed foreman.
*   **Delays (general):** Occurs "usually" (e.g., passenger emergencies).
    *   **Process:** Note the exact time and reason in the logbook.
