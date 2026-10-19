# Customer Service

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

## 2026-10-04T10:38:57.841Z - When boards, announcements and the app disagree, who handles the passenger and who fixes the data?

Roles: station-manager, customer-service
Transcript: transcripts/station-manager+customer-service-2026-10-04T10-38-57-839Z.md

### Needs

* **Station-Manager:**
  * Direct ability to verify the "source of truth" without relying on a mess of phone calls or radioing the control room/signal box.
  * Local Override Authority to instantly post "Service Cancelled" or "Platform Change" notices directly from their station terminal.
  * Clear system health indicators on physical display boards (e.g., status lights/icons indicating delayed feeds or network drops).
  * Direct synchronization ensuring app and platform displays pull from the exact same source.
  * Strict security controls, concurrency locking, and audit logging to safely govern local overrides.
* **Customer-Service:**
  * A real-time, single authoritative Live Incident Feed displaying disruption updates and operational reasons directly from station managers.
  * An escalation mechanism ("Report Data Error" button) to flag discrepancies directly to Central Controllers/Operations.
  * Explicit visibility into intermediate system states (e.g., "Local Cancellation Pending Central Confirmation") showing staff details and timestamps.
  * Pre-approved trilingual messaging (Sinhala, Tamil, English) for customer communication.
  * A provisional claim logging workflow to capture passenger details without committing to unauthorized refunds.
  * Automated escalation if Central Operations does not reconcile a local override within 15 minutes.

---

### Pain Points

* **Station-Manager:**
  * Verification requires time-consuming phone calls to signal boxes and radio checks with control rooms during crowded, high-stress peak hours (e.g., 6:00 PM on a Friday).
  * Display boards present stale data or "ghost trains" during network outages instead of indicating a system failure.
  * Inconsistencies between mobile apps and physical station boards lead to crowds of angry commuters and risk loss of crowd control.
* **Customer-Service:**
  * CS agents frequently learn of disruptions from frantic passengers before official updates reach internal dashboards.
  * Internal dashboards pull outdated schedules showing "On Time" while passengers look at empty tracks, placing staff in a humiliating position that looks incompetent.
  * No existing mechanism to flag data errors to Operations.
  * Station managers canceling trains locally before Central Operations updates the ticketing backend causes passengers to demand immediate refunds, putting CS at risk of making unauthorized payout promises.

---

### Failure Scenarios

* **Ghost Train & Display Desynchronization:** Network drops cause platform boards to show an outdated departure while the mobile app lists it as cancelled, resulting in angry crowds during peak hours.
* **Customer Service Blindness:** CS tells a passenger a train is "On Time" based on scheduled data while the passenger is physically standing at an empty platform at Colombo Fort.
* **Crowd Stampede (Concurrent Edit Failure):** Two station staff members enter conflicting platform numbers simultaneously without system locking, prompting thousands of commuters to stampede across footbridges.
* **Silent Overwrite:** Central Operations silently reverts or overwrites a local station manager's safety action on a display without contacting the station manager.
* **Unauthorized Refund / Compensation Mismatch:** A local cancellation is displayed on platform screens, triggering passenger refund demands while backend ticketing records still show the train as active, leaving CS without authorized refund rules.

---

### Requirements Wanted (with Priority)

* **High / Safety-Critical:**
  * **Role and Duty-Based Access Control:** Restrict override capabilities strictly to active Shift Supervisors or Station Managers logged into that specific station (e.g., Colombo Fort).
  * **Single-Editor Platform Locking:** Automatically lock a platform display to a single user during an update and show an active lock notice with the editor’s name to others.
  * **Mandatory Audit Logging with Reason Selection:** Log user ID, timestamp, and require selection from approved operational reasons (e.g., "Track Obstruction", "Signal Delay").
  * **Local Override Authority:** Terminal capability for Station Managers to override platform boards immediately without waiting for central office approval.
  * **Central Controller Alerting:** Automatically notify Central Operations of local overrides while preventing central control from silently overwriting local safety actions.
* **High Priority:**
  * **Real-time Synchronization:** App and platform boards must pull simultaneously from the same authoritative data source.
  * **System Health Indicators:** Display visual alerts (status light/icon) on boards stating "Data feed delayed—verify locally" or indicating network failure.
  * **Live Incident Feed:** Real-time stream on CS dashboards showing ground updates with operational reasons.
  * **Discrepancy Escalation ("Report Data Error"):** Button on CS dashboards to flag mismatches directly to Central Controllers.
  * **Intermediate State Display:** CS dashboard must show "Local Cancellation Pending Central Confirmation" with user details and timestamps.
  * **Approved Trilingual Messaging:** Pre-scripted copy in Sinhala, Tamil, and English for CS communications.
  * **Provisional Claim Logging:** Workflow allowing CS to log passenger ticket and contact information without authorizing immediate payouts.
  * **Automated Reconciliation Escalation:** Trigger an alert to Central Controllers if Central Ops has not reconciled a local override within 15 minutes.

---

### Conflicts Between Roles

* **Station Management vs. Central Operations:**
  * Station Managers require immediate local override authority to manage safety and crowd control locally, whereas Central Operations risks overwriting local actions or delaying official system-wide updates.
* **Station Staff vs. Station Staff (Operational Concurrency):**
  * Multiple local staff attempting to update the same platform display simultaneously without locking risks posting conflicting instructions to commuters.
* **Station Management vs. Customer Service & Central Ticketing:**
  * Station Managers taking rapid action to mark trains "Cancelled" locally conflicts with Central Ops' delayed backend ticketing updates, leaving Customer Service exposed to customer demands for refunds without backend validation.

---

### Open Questions / Assumptions

* **Missing Compensation & Refund Policy:** The operational brief completely leaves out provisional refund workflows and compensation rules when local station overrides conflict with central ticketing records.
* **Central Override Governance:** The exact process for how Central Operations is permitted to validate, resolve, or communicate before modifying a Station Manager’s local safety override remains unspecified.
* **Ticketing Backend Reconciliation SLA:** The exact protocol Central Operations must follow to officially validate local overrides within or beyond the proposed 15-minute window is not defined.

## 2026-10-04T10:49:28.370Z - What passenger and operational data may customer service see, and how is it audited?

Roles: security-engineer, customer-service
Transcript: transcripts/security-engineer+customer-service-2026-10-04T10-49-28-368Z.md

Here is the business analysis based on the transcript of the discussion between the Security Engineer and Customer Service Representative.

---

### Needs
* **Security Controls & Access Restriction:** 
  * Need to restrict user detail API access strictly to the support platform (no public endpoints).
  * Need Attribute-Based Access Control (ABAC) to ensure customer service representatives can only view passenger data when there is an active, open ticket assigned to them.
  * Need write-only, immutable audit logs shipped off-host in real-time to a secure, centralized logging server to prevent tampering.
  * Need PII data masking to ensure representatives only see "need-to-know" data (e.g., masking phone numbers if not required, but keeping class and fare visible for refunds).
* **Proxy Support Handling:** 
  * Need a mechanism to temporarily link a "support requester" (e.g., a parent booking for a child, or a tour guide booking for a group) to a "passenger" or "booking reference."
* **Identity Proofing & Validation:** 
  * Need a robust method to verify the passenger’s identity (identity proofing) to prevent unauthorized data exfiltration.
  * Need a "challenge-response" mechanism in the system consisting of safe, non-PII questions (e.g., confirming destination station or approximate fare paid in LKR) to verify callers.
* **Offline Procedures:** 
  * Need manual bypass procedures and offline validation steps to assist passengers who have no mobile signal or do not use the app.

---

### Pain Points
* **Proxy Relationships:** Dealing with third-party callers (e.g., a father in Gampaha calling about his daughter's delayed train from Kandy, or tourists booking for groups) is highly difficult. Under rigid access rules, agents cannot view booking details because the account belongs to someone else.
* **Slow Verification Over Poor Signal:** Forcing agents to ask passengers to read out long booking references or PNRs is slow and frustrating, especially when mobile signals are dropping.
* **Infrastructure and Demographic Limitations:** 
  * Constant mobile signal drops on crowded 3rd-class commuter trains (e.g., from Veyangoda or in the Rambukkana tunnels) prevent passengers from receiving SMS or using apps.
  * Older passengers do not use the mobile app and rely entirely on physical tickets bought at station counters.
* **Incomplete Brief:** The current system brief completely ignores data masking, identity proofing, proxy relationships, and offline validation.

---

### Failure Scenarios
* **Massive Data Scraping:** A compromised customer service representative account searches and scrapes the entire passenger database because search access is unrestricted.
* **Log Tampering:** An attacker or malicious insider accesses records or modifies tickets to gain unauthorized access, then deletes or alters the access logs to cover their tracks.
* **Service Lockout / Customer Frustration:** A rigid ABAC system locks out representatives from viewing proxy bookings, leaving them unable to help desperate passengers and forcing them to turn customers away.
* **Signal Drop Failure:** A passenger is stuck in a tunnel (e.g., Rambukkana tunnels) with no signal, cannot receive a security consent link or SMS, and is subjected to a "hard lockout" by the system, leaving the agent unable to assist them.
* **Social Engineering / Data Exfiltration:** A caller who simply knows a PNR exploits the customer service representative to exfiltrate passenger details because there is no strong passenger identity proofing.
* **Unilateral Privilege Escalation:** A representative uses a weak "break-glass" tool to bypass security controls unilaterally without supervisor oversight, creating a permanent security loophole.

---

### Requirements Wanted (with Priority)

#### **High Priority**
* **Attribute-Based Access Control (ABAC):** Restrict passenger data access to representatives only when they have an active, open ticket assigned to them.
* **Immutable Audit Logs:** Implement real-time, off-host, write-only centralized logging for all data access and ticket modifications.
* **Proxy Authorization Mechanism:** Provide a way to temporarily link support requesters to passengers or booking references.
* **Dual-Control "Break-Glass" Protocol:** Any emergency ABAC bypass must require a supervisor’s secondary authorization code, trigger an immediate high-severity alert, and log every second of the access.

#### **Medium Priority**
* **PII Data Masking with "Need-to-Know" Exceptions:** Mask sensitive PII (like phone numbers) by default, but allow access to specific operational fields (like travel class and fare paid) necessary to process refunds.
* **API Restriction:** Restrict the passenger detail API to the support platform, blocking any public endpoint access.
* **Challenge-Response Verification System:** Build a system-generated challenge mechanism using non-PII questions (e.g., destination, fare paid in LKR) for agents to verify callers.
* **Delegated Authorization Workflow:** Implement a time-bound consent verification process via the passenger’s app or a secure link for proxy requests (to be used when signal is available).

#### **Low Priority**
* **Manual Offline Bypass Procedures:** Documented and logged manual override protocols for when passengers have zero connectivity and cannot use digital verification.

---

### Conflicts Between Roles

* **App-Based Consent vs. Real-World Connectivity:** 
  * *Security* wants a "delegated authorization workflow" where the ticket owner grants time-bound consent via an app or secure link. 
  * *Customer Service* argues this is unusable for passengers on commuter trains with dropping signals, or for older passengers who only use physical tickets and do not have the app.
* **Data Masking vs. Operational Utility:** 
  * *Security* advocates for maximum data masking of PII to protect passenger data. 
  * *Customer Service* argues that over-masking (e.g., masking the fare amount or travel class) will prevent them from performing basic tasks like processing refunds.
* **"Break-Glass" Overrides vs. Security Vulnerabilities:** 
  * *Customer Service* demands a flexible override/bypass mechanism to prevent "hard lockouts" when passengers are offline. 
  * *Security* views "authorized overrides" as dangerous privilege escalation ("back doors") and insists on strict dual-control authorization and heavy logging.

---

### Open Questions / Assumptions

* **Identity Proofing:** How do we reliably verify the identity of a caller (e.g., a father calling from Gampaha) if we cannot rely on digital app links or SMS due to poor signal?
* **Offline Escalation Path:** What is the specific escalation path for a representative when a passenger is completely offline (e.g., in the Rambukkana tunnels) and fails automated system checks? Do manual bypasses exist, and how are they governed?
* **Brief Omissions:** Why does the current project brief completely omit details regarding data masking, identity proofing, offline validation, and proxy/third-party support relationships?

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
