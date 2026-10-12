# QA Engineer

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

## 2026-10-04T10:51:20.036Z - Which five end-to-end flows matter most, and what are the acceptance criteria?

Roles: qa-engineer, product-owner
Transcript: transcripts/qa-engineer+product-owner-2026-10-04T10-51-20-034Z.md

### Needs
*   **Unique Idempotency Key:** Mandatory for every transaction to prevent double-charging.
*   **Atomic Transactions:** Payment and seat assignment must be treated as a single atomic operation.
*   **Reconciliation Service:** An automated system to handle reversals when payment succeeds but seat assignment fails.
*   **API Specifications:** Precise contract (status codes, response structures) for sync states.
*   **Offline/Recovery Mechanism:** A way for the app to poll the backend for status using a transaction ID upon restoring connectivity.

### Pain Points
*   **Network Instability:** Handling transactions for commuters at Colombo Fort with poor mobile data.
*   **Support Desk Load:** Avoiding manual intervention for refund requests, which is deemed unsustainable.
*   **Ghost Payments:** The risk of a user being charged without receiving a ticket or QR code.
*   **UX Frustration:** Users potentially closing the app during "Processing" states or receiving "Seat Gone" messages after investing time in checkout.

### Failure Scenarios
*   **Double-Tap/Race Conditions:** User clicks "Buy" twice during lag.
*   **Gateway Timeouts:** Payment gateway hangs during the checkout process.
*   **Inventory Contention:** Two users attempt to claim the final seat simultaneously.
*   **Partial Sync:** Payment is processed, but connection is lost before the ticket is rendered.
*   **Session/Hold Expiry:** The 5-minute "soft hold" expires while the user is in the process of paying.
*   **Atomic Failure:** Payment succeeds, but the inventory service fails to lock the seat.

### Requirements Wanted (with priority)
1.  **High:** **Automatic Reconciliation:** System must trigger an instant reversal if ticket issuance fails (No manual intervention).
2.  **High:** **5-Minute "Soft Hold":** Seat is locked for 5 minutes once checkout initiates to ensure user trust.
3.  **High:** **Atomic Logic:** No "pending" status should exist for more than 60 seconds; if inventory fails, payment must be voided/refunded.
4.  **Medium:** **Polling Mechanism:** App must poll the backend for status using the transaction ID to recover from "Partial Sync" scenarios.
5.  **Medium:** **"Best Available" Allocation:** Simplified inventory model to reduce complexity for the MVP.
6.  **Medium:** **Offline UX:** Clear messaging ("Payment processing—please do not close the app") and a "Retry" button.

### Conflicts Between Roles
*   **QA-Engineer vs. Product-Owner (Scope/Rigour):** The QA-Engineer demands precise technical numbers (e.g., timeout thresholds, specific API status codes) and highlights "nightmare" edge cases, while the Product-Owner pushes to simplify the business model (using "Best Available" and rejecting complex multi-seat booking) to ensure the MVP is grounded and manageable.

### Open Questions / Assumptions
*   **Timeout Thresholds:** The QA-Engineer requested specific numbers for gateway timeouts (10s vs 30s), which have not been explicitly set yet.
*   **Recovery Path:** What happens if the user closes the app during the "Payment processing" phase? (The QA-Engineer flagged this as an open issue).
*   **API Contract:** The exact status codes (e.g., 200 vs 202) for the "Payment received, seat pending" state remain to be defined in the API specs.

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
