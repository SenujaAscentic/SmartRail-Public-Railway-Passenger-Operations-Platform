# Catering Provider

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

## 2026-10-04T10:47:44.556Z - Who owns menus, payment, refunds and missed collections?

Roles: catering-provider, product-owner
Transcript: transcripts/catering-provider+product-owner-2026-10-04T10-47-44-554Z.md

### Needs
* **Catering Provider:**
  * Direct payment processing through their own merchant gateway or Cash on Delivery (COD) in LKR at the platform to avoid multi-week railway settlement cycles.
  * Guaranteed full payment once food is prepared and the cut-off time passes, regardless of train delays.
  * A strict two-hour cut-off time prior to train arrival at the collection station to prevent last-minute cancellations.
  * Secure receipt of order references without exposure or leakage of passenger profiles.
  * Receipt of train delay updates from SmartRail so platform staff know when a service is running behind schedule.
* **Product Owner / SmartRail:**
  * Avoidance of scope creep, financial audit liabilities, and payment ownership risks in the MVP due to a tight budget and small team.
  * A lightweight hand-off mechanism passing only necessary journey details (train ID, collection station, scheduled ETA).
  * Assurance that SmartRail does not absorb or underwrite private vendor losses or spoiled food costs.
  * Timely launch focused on core priorities (train tracking, schedule accuracy, and disruption alerts).

---

### Pain Points
* **Catering Provider:**
  * Tight operational margins exacerbated by high food costs in Colombo.
  * Inability to tolerate delays of several weeks under SmartRail’s bureaucratic financial settlement cycles.
  * High risk of unsellable, wasted food when meals spoil due to operational train delays or passenger no-shows.
* **Product Owner:**
  * Small engineering team and limited budget preventing the implementation of complex payment reconciliation systems.

---

### Failure Scenarios
* **Operational Train Delay Spoilage:** A train (such as the *Podi Menike*) is delayed (e.g., by three hours at Rambukkana), causing prepared hot meals to spoil.
* **Passenger COD No-Show:** A passenger orders hot food (e.g., chicken lamprais) for collection at a station (e.g., Gampaha) but misses their departure train (e.g., at Maradana), leaving the catering provider with uncollected, unrecoverable, and unsellable food.
* **MVP Delivery Failure / Deferral:** The catering provider’s technical team fails to implement the required payment redirect integration prior to the MVP launch date, causing the entire catering feature to be cut from the initial release.

---

### Requirements Wanted (with Priority)

#### Must (MVP)
* **External Payment Redirect:** SmartRail must redirect the passenger from the app to the catering provider's external portal/gateway for direct payment in LKR (*100% owned and operated by the catering provider*).
* **Journey Data Hand-off:** SmartRail must pass core journey details to the catering portal:
  * Train service ID.
  * Collection station (e.g., Gampaha, Colombo Fort).
  * Scheduled ETA.
* **Delay Update Feeds:** SmartRail must transmit train delay updates to the catering provider’s platform.
* **Policy Enforcement on Vendor Platform:** The catering provider must enforce their two-hour cut-off window, manage all refunds, and handle payment disputes on their own platform.
* **Secure Order Reference Passing:** Mechanism to transmit order references to the vendor gateway without leaking passenger personal profiles.

#### Deferred / Excluded
* **Unified Checkout & Downstream Settlement:** Deferred; SmartRail will not collect funds or perform downstream reconciliation for catering in this release.
* **Cash on Delivery (COD):** Excluded / Off the table; rejected due to dispute over who absorbs no-show liabilities.
* **Railway Underwriting of Unpaid/Spoiled Food:** Excluded; SmartRail will not pay for vendor no-shows or food spoilage.
* **Catering Module (Conditional Deferral):** Deferred entirely to Release 2 if the catering provider cannot support the external redirect integration in time for the MVP launch.

---

### Conflicts Between Roles
* **Liability for No-Shows and Spoilage:** 
  * The catering provider demanded that SmartRail underwrite 100% of the financial risk for COD no-shows via monthly invoices, as well as pay for food spoiled by train delays.
  * The Product Owner flatly refused, stating executive leadership will not approve covering private vendor losses, leading to the complete elimination of COD.
* **Payment Architecture vs. Delivery Timeline:**
  * The Product Owner sought the simplest integration to launch quickly (initially proposing COD or a simple hand-off) without risking platform delays.
  * The catering provider refused COD unless compensated for no-shows, requiring an online redirect from day one.
  * The Product Owner counter-leveraged that if the catering provider's technical team cannot deliver this redirect by the MVP launch date, the feature will be deferred entirely to Release 2.

---

### Open Questions / Assumptions
* **Open Questions:**
  * Can the catering provider’s technical team build and launch the external redirect integration in time for the SmartRail MVP release date?
  * What technical mechanism will SmartRail use to securely pass the order reference and journey details without leaking passenger profile data?
* **Assumptions:**
  * The catering provider assumes that with upfront online payments, any passenger refund requests resulting from missed trains will be settled between the passenger and SmartRail.

## 2026-10-04T10:48:14.957Z - How is the integration between SmartRail and the catering platform secured, and what passenger data is shared?

Roles: catering-provider, security-engineer
Transcript: transcripts/catering-provider+security-engineer-2026-10-04T10-48-14-955Z.md

### Needs
* Order fulfillment data transmission containing only an order reference number, train service number, collection station, and trip identifier (carriage/seat/compartment), without any PII.
* Direct online payment processing via the catering platform's payment gateway at the time of ordering (no cash on delivery on board moving trains).
* Secure API communication and webhook mechanisms robust against mobile signal drops around tunnels on the Main Line.
* Real-time updates of menu, prices, and stock availability controlled entirely by the catering provider.
* Clear operational and liability processes for missed trains, cancellations, service disruptions, and wasted perishable stock.
* Secure audit logging and tracking for financial liability disputes, schedule changes, delays, and catering state transitions.

### Pain points
* Handling PII creates unnecessary data privacy liabilities under Sri Lankan data protection norms.
* Cash handling on crowded carriages (e.g., Coastal Line commuter runs) is impractical and unsafe.
* Mobile signals drop around tunnels on the Main Line, risking webhook failures and duplicate order preparation due to delayed or retried payloads.
* Potential for external integration endpoints to suffer from injection and replay attacks when signal bounces back.
* Operational vulnerability regarding compromised staff credentials and shared tablets on station platforms and in train cabs.
* Financial loss risks associated with wasted perishable stock (e.g., hot rice and curry or short eats) when trains are abruptly cancelled or schedules are delayed/wrong without early notice.
* Risk of financial fraud, contractor disputes, and exposure of internal schedules/vendor contracts if audit trails leak or are tampered with.

### Failure scenarios
* An attacker sniffs traffic or spoofs an unsecured webhook endpoint to flood the catering partner with fake orders for empty seats on the up-country Main Line.
* Mobile data drops around tunnels near Rambukkana, causing signal bounces that duplicate webhook payloads and risk duplicate order preparation for limited perishable stock.
* A driver's device or station terminal is left unattended, giving an attacker open access to operational APIs via compromised staff credentials or shared tablets.
* A passenger misses their train (e.g., stuck in peak-hour traffic at Colombo Fort) or a train is cancelled due to a landslide near Rambukkana or a sudden strike *after* the cut-off time, leaving uncertainty over who absorbs the financial loss for already-packed perishable food.
* SmartRail's schedule data is wrong or delayed without early notice, resulting in wasted catering stock with unclear compensation ownership.
* Audit logs or dispute records are exposed or tampered with, leading to financial fraud, contractor disputes, and leakage of vendor contracts or internal operational vulnerabilities.

### Requirements wanted (with priority)
1. **Mutual TLS (mTLS) for all service-to-service communication** with the catering platform (No plain API keys over HTTPS). *(Priority: Non-negotiable / High)*
2. **Cryptographic webhook signatures (HMAC-SHA256 with timestamp verification)** to defeat replay attacks during intermittent signal drops. *(Priority: Non-negotiable / High)*
3. **Strict rate limiting and payload validation** on every inbound webhook. *(Priority: Non-negotiable / High)*
4. **Zero PII transmitted to third parties** (Data minimisation). *(Priority: Non-negotiable / High)*
5. **Immutable Audit Logging:** Every schedule change, delay notification, and catering state transition written to an append-only, tamper-evident audit store with no manual overrides without logged operational sign-off. *(Priority: Non-negotiable)*
6. **Strict Data Minimisation in Logs:** Audit logs and error traces must never contain PII, raw payment tokens, or internal infrastructure details (log only hashed order references, timestamps, and event codes). *(Priority: Non-negotiable)*
7. **Separation of Duties:** Staff member who approves a schedule change or disruption alert must not be the same person who processes vendor compensation payouts. *(Priority: Non-negotiable)*

### Conflicts between roles
* **Catering Provider vs. SmartRail (Schedule & Liability):** The catering provider demands compensation from SmartRail for wasted perishable stock if train schedules are wrong, delayed without early notice, or abruptly cancelled by the railway after food is packed. This creates a financial liability conflict over who covers the cost of wasted food caused by railway delays (e.g., landslides near Rambukkana).

### Open questions / assumptions
* How does SmartRail plan to authenticate API calls and handle exact retry and idempotency policies for webhooks when mobile signals drop?
* Who covers the financial liability and vendor compensation payouts when SmartRail's schedule data is wrong or delayed, leading to wasted perishable stock?

## 2026-10-04T10:52:11.793Z - How should a passenger order food for a stop on the journey, and what happens if the train is late or the stop is missed?

Roles: occasional-commuter, catering-provider
Transcript: transcripts/occasional-commuter+catering-provider-2026-10-04T10-52-11-791Z.md

### Needs
* **Occasional-commuter:**
  * Immediate notification via the app if an order is affected by a delay.
  * Automatic refunds for cancelled orders or bypassed stations without needing a claims process, forms, or support tickets.
  * A one-tap option to "cancel and refund" if a train is delayed by an hour.
  * In-app warnings before pulling into a station if the stop is shortened (e.g., to 30 seconds).
  * Safe alternatives for short stops (automatic rerouting to the final destination or automatic cancellation/refund) to avoid rushing or missing train doors.
* **Catering-provider:**
  * Immediate notification to the kitchen if a passenger taps "cancel" or if the train bypasses the station, to prevent wasted time and ingredients.
  * A rock-solid automatic webhook that does not arrive late, out of order, or duplicate due to weak signals.
  * An absolute, definitive cutoff time (e.g., 45 minutes before scheduled arrival) after which an order is locked and ingredients are committed.
  * Clear rules on liability regarding who reimburses the kitchen for wasted stock when the railway messes up the timetable.
  * Clean synchronization with the payment gateway for automated refunds to avoid getting stuck chasing transaction fees.
  * Respect for kitchen capacity limits, fixed batch quantities, and geographical footprints (no branch/staff at a station means no delivery).

---

### Pain points
* **Occasional-commuter:**
  * Stress about missing connections, being late, and not knowing where to go next at unfamiliar stations.
  * Lack of mental energy to navigate support tickets for small food items.
  * Food becoming cold or stale due to hour-long train delays.
  * Anxiety about running down platforms, carrying luggage, and worrying about doors closing during a shortened 30-second stop.
  * Panic when traveling with out-of-town friends.
* **Catering-provider:**
  * Wasted food and ingredients ending up in the bin due to delayed notifications, with no clarity on who covers the loss.
  * Risk of staff being left guessing due to weak signal issues near stations causing late, out-of-order, or duplicate webhook notifications.
  * Kitchen crews drowning from unpredicted, last-minute influxes of diverted/transferred meals dumped onto them at different stations.

---

### Failure scenarios
* A train is delayed, making the ordered food cold or stale by arrival.
* A weak signal near a station causes automated webhooks to arrive late, out of order, or as duplicates, leaving kitchen staff guessing.
* A train stop is shortened to 30 seconds, causing the commuter to rush down the platform while carrying luggage and risking getting locked out as the doors close.
* An order is automatically rerouted to a final destination station where the catering provider does not have a branch or staff, resulting in an unfulfillable order.
* Unannounced, diverted meals are suddenly dumped onto a kitchen that prepared food according to a strict schedule and fixed batch quantities, overwhelming the crew.

---

### Requirements wanted (with priority)
* **Immediate delay/bypass notification in the app:** High
* **Automatic refund processing for cancellations/bypassed stops (no forms/claims process):** High
* **One-tap "cancel and refund" option for delayed trains:** High
* **Immediate kitchen notification for cancellations/bypassed stops via rock-solid webhooks:** High
* **Definitive cutoff time (e.g., 45 minutes before arrival) to lock orders:** High
* **Clear liability rules for who reimburses wasted stock caused by railway timetable errors:** Medium
* **Payment gateway synchronization for automated refunds without transaction fee loss:** Medium
* **Advance warning in-app for shortened stops (e.g., 30-second stops):** High
* **Automatic cancellation and refund if a collection stop is too short (disallowing rerouting to stations without branches):** High

---

### Conflicts between roles
* **Rerouting vs. Kitchen Capacity & Geography:** The commuter expects the app to automatically reroute an order to the final destination if a stop is too short. The catering provider explicitly rejects this, stating they cannot handle unannounced diverted meals and cannot fulfill orders at stations where they have no branch or staff.
* **Liability for Wasted Stock:** The catering provider demands to know who covers the financial loss of wasted food and ingredients when the railway messes up the timetable, whereas the commuter expects the system to simply refund them without hassle, leaving a gap in who bears the supplier's cost.

---

### Open questions / assumptions
* **Assumptions:**
  * Passengers pay upfront through the catering provider's online platform.
  * The system possesses the technical capability to track train delays, station bypasses, and shortened stop durations in real-time.
* **Open Questions:**
  * Who covers the financial loss of wasted kitchen stock when the railway timetable is messed up?
  * How can the system guarantee reliable webhook delivery near stations with weak cellular signals?

## 2026-10-04T10:52:32.848Z - Where and how do passengers collect food at the station, and who is responsible during delays?

Roles: catering-provider, station-manager
Transcript: transcripts/catering-provider+station-manager-2026-10-04T10-52-32-847Z.md

### Needs
* **Catering Provider:**
  * Clear, upfront payment terms (digital payments/online payment gateway settled via contract, no cash on delivery on trains).
  * A strict, automated cut-off time for locking and billing orders when train delays occur.
  * Direct handovers from their own delivery runner to the passenger at the train door (bypassing station staff).
* **Station Manager:**
  * Train punctuality and proper platform discipline from the control room.
  * Safe, clear, and uncrowded platforms focused strictly on boarding and alighting.
  * Security control over unauthorized individuals loitering near the tracks.
  * Proper registration of catering staff if they are allowed on station premises, or handovers restricted to outside the ticket gates in the main concourse.

---

### Pain points
* **Catering Provider:**
  * Perishable fresh food (rice, curries, short eats) has a very short shelf life and cannot be resold once it leaves their control.
  * Sudden train delays or cancellations after kitchen preparation result in wasted food and financial loss.
  * Managing cash collection on crowded, moving commuter trains is completely impractical.
* **Station Manager:**
  * Severe station overcrowding (e.g., packed Friday evenings at Colombo Fort or Maradana) with staff already overwhelmed managing crowds and tracks.
  * Lack of physical space/spare rooms for holding catering drop-offs; storing food near ticket counters causes hygiene and pest issues.
  * Last-minute platform changes due to control room pathing errors.
  * Safety hazards caused by delivery runners loitering at the platform edge near active tracks during peak commuter surges.

---

### Failure scenarios
* **Scenario 1:** A train suffers a sudden delay or cancellation *after* the catering kitchen has already prepared and dispatched food to the station pickup point, leaving perishable items to spoil with no clear mechanism for who absorbs the cost.
* **Scenario 2:** Catering delivery boxes are left unattended on a crowded platform, resulting in them being knocked over or stolen within minutes.
* **Scenario 3:** Station staff are forced to hold perishable food, leading to hygiene nightmares behind the ticket counter and liability disputes over spoiled or lost orders.
* **Scenario 4:** Last-minute platform changes require station staff or others to run across four platforms carrying food boxes while trying to prevent passengers from falling onto the tracks.
* **Scenario 5:** Unauthorized catering delivery runners weave through dense commuter crowds and loiter at the platform edge, causing safety hazards, blocking passenger flow, and triggering station security interventions.

---

### Requirements wanted (with priority)
1. **Automated Delay Cut-Off Window (High Priority / Catering Provider):** A strict, automated cut-off time (set at two hours before the train's scheduled departure from the originating station) to lock kitchen production and bill orders regardless of subsequent train delays.
2. **Upfront Digital Payment Gateway (High Priority / Catering Provider):** Integration with the SmartRail app for upfront online payments and digital/integrated gateway settlements, eliminating cash on delivery.
3. **Delivery Runner Registration or Concourse-Only Handovers (High Priority / Station Manager):** Requirement for catering delivery runners to either register properly with station management or restrict food handovers to outside the ticket gates in the main concourse (prohibiting platform-edge handovers).
4. **Insulated Holding Units & Representation (Medium Priority / Station Manager):** If station holding is ever utilized, the catering provider must supply insulated holding units and their own station representative to manage collections (refusing station staff liability).

---

### Conflicts between roles
* **Platform Access & Safety vs. Direct Delivery:** The catering provider insists on having delivery runners wait directly by train doors on platforms to hand food to passengers. The station manager strictly opposes this, citing severe safety hazards, blocked passenger flows, and the treatment of unregistered runners near tracks as security risks. The station manager demands handovers happen outside ticket gates in the concourse.
* **Storage & Operational Liability:** The catering provider expects the railway/station to handle or absorb the cost of food during delays if orders are dispatched. The station manager refuses to take any liability for spoiled or lost food, states they have zero space or inventory staff, and refuses to let station staff touch the food boxes.
* **Payment Methods:** The catering provider explicitly refuses to manage cash collection on moving trains and demands upfront digital payment via the app. (Implicit conflict if the app/railway platform does not currently support this seamless integration).

---

### Open questions / assumptions
* **Assumptions:**
  * The SmartRail app exists, but its current payment gateway capabilities regarding upfront settlements for external vendors are unconfirmed.
  * The catering provider operates with delivery runners who can reach stations independently.
* **Open Questions:**
  * How will the SmartRail app enforce the two-hour automated cut-off time linked to real-time railway delay announcements?
  * Will the catering provider agree to move their handover point entirely outside the ticket gates to the main concourse to satisfy station security?

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
