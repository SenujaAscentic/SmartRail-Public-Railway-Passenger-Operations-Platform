# Product Owner

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

## 2026-10-04T10:50:35.927Z - What passenger data do we collect, how long do we keep it, and what must not be logged?

Roles: security-engineer, product-owner
Transcript: transcripts/security-engineer+product-owner-2026-10-04T10-50-35-926Z.md

## Needs
* Exclude raw payment credentials and PII (National Identity Card numbers, phone numbers) from application and system logs.
* Exclude live GPS coordinates and real-time passenger location tracking from system logs.
* Log anonymised event IDs and tokenised transaction references instead of raw data.
* Store tokenised ticket and journey histories in a database (not raw log files) to enable customer support dispute resolution (ticket bought, service booked, class, validation scans at station gates like Colombo Fort or Maradana).
* Role-based access control (RBAC) where customer support staff see only tokenised IDs.
* Dual-authorization sign-off or senior operations manager's override to de-tokenise and reveal a passenger's name or phone number.
* Immutable, append-only audit trails for every travel history lookup (recording who accessed it, ticket ID queried, and dispute reference ticket attached).
* Automated alerts to security operations if an agent looks up a profile without an active customer complaint ticket.
* Strong encryption at rest for historical journey data in the database, with regularly rotated keys separated from application operational data.
* A lawful hold mechanism ("Hold Flag") in the database admin console to freeze the 90-day auto-purge countdown for specific records.
* Clear logging of who applied a legal hold and why, tied to an official ticket reference number from the legal team.
* Database architecture that supports record-level retention exceptions without breaking automated privacy cleanup jobs.

## Pain points
* If operations or third-party vendors can read log aggregation tools, raw payment credentials, ID numbers, phone numbers, and live GPS coordinates are exposed.
* Logging real-time passenger tracking or full credit card numbers means one compromised log store exposes the entire user base.
* Pulling up 90 days of gate scans for daily commuters (e.g., Gampaha to Colombo Fort) creates raw surveillance data.
* Unprotected support portals risk becoming a stalking tool.
* Blanket long-term retention of everyone's daily commute history across the Main Line or Coastal Line is a massive liability.

## Failure scenarios
* A compromised log store exposes the entire user base because raw payment credentials or real-time passenger tracking were logged.
* A support portal acts as a stalking tool because strict access controls and role separation are not enforced on historical movement data.

## Requirements wanted (with priority)

### Product Owner Requirements:
* **Must Have:** Securely store tokenised ticket and journey histories in the database—not raw log files—for customer support dispute resolution regarding delayed intercity trains or ticketing errors. (Priority: High)
* **Should Have:** A clear data retention policy keeping passenger travel history for a maximum of 90 days for dispute resolution, after which it must be permanently purged or fully anonymised. (Priority: Medium)
* **Deferred / Out of Scope:** Real-time passenger location tracking in support logs (the system tracks the train's position for operations, not individual passengers). (Priority: Low)
* **Must Have:** A legal/fraud "Hold Flag" in the database admin console allowing authorized compliance officers to place a retention lock on a specific tokenised account or ticket reference during an official subpoena, active fraud investigation, or legal dispute, freezing the 90-day auto-purge countdown for that exact dataset only. (Priority: High)
* **Should Have:** Clear logging on who applied the legal hold and why, tied to an official ticket reference number from the legal team. (Priority: Medium)
* **Deferred / Out of Scope:** Keeping all passenger records indefinitely "just in case" (blanket long-term retention). (Priority: Low)

### Security Engineer Requirements:
* **Least Privilege & Role Separation:** Customer support staff must never see raw passenger identities linked directly to historical movement; support agents see only tokenised IDs. De-tokenisation requires a dual-authorization sign-off or senior operations manager's override. (Priority: High)
* **Mandatory Audit Trails:** Immutable, append-only audit log for every single lookup of travel history (who accessed it, ticket ID queried, dispute reference ticket attached), with automated alerts to security operations for lookups without an active complaint ticket. (Priority: High)
* **Encryption at Rest:** Historical journey data sitting in the database for 90 days must be strongly encrypted with regularly rotated keys, separate from application operational data. (Priority: High)

## Conflicts between roles
* **Security vs. Support Operations:** Security raises concerns that allowing customer support to view 90 days of station gate scans (to resolve disputes) effectively reconstructs a passenger's physical travel patterns and creates raw surveillance data. This is resolved by implementing strict role-based access control, dual-authorization/manager overrides for de-tokenisation, and mandatory immutable audit trails with automated alerts.

## Open questions / assumptions
* **Assumptions:** 
  * Operations or third-party vendors have access to log aggregation tools.
  * Customer support requires transactional and journey history (tickets bought, services booked, class, validation scans at gates like Colombo Fort or Maradana) for dispute resolution.
* **Open Questions:**
  * Who specifically has access to our log aggregation tools?
  * Who can call the lookup tool, and who audits the auditors?

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

## 2026-10-04T10:53:06.873Z - Which passenger features must be in the first release, and what can wait?

Roles: product-owner, daily-commuter, occasional-commuter
Transcript: transcripts/product-owner+daily-commuter+occasional-commuter-2026-10-04T10-53-06-848Z.md

This report summarizes the discussion regarding the passenger-facing mobile application and its integration with operational systems.

### Needs
*   **Real-time status:** Clear, accurate notifications regarding delays (time and reason).
*   **Platform certainty:** Precise, synchronized platform information to prevent last-minute movement.
*   **Alternative routing:** Guidance on how to reach a destination if a train is cancelled.
*   **Human support:** A clear path to human assistance if digital guidance fails or alternatives are unavailable.
*   **Data integrity:** A "Single Point of Truth" for all station data to ensure consistency between digital and physical displays.

### Pain Points
*   **"Information Vacuum":** Lack of communication during disruptions leading to crowding and anxiety.
*   **App vs. Board Mismatch:** Contradictory information between the app and physical signage destroys user trust.
*   **Connectivity Gaps:** Stale notifications received after exiting tunnels or regaining signal.
*   **Uncertainty for Occasional Users:** Lack of familiarity with station layouts and alternative transit options.

### Failure Scenarios
*   **Synchronization Failure:** The app displays a different platform than the station board.
*   **Stale Notifications:** The app sends push alerts for platform changes that occurred while the user was offline/in a tunnel.
*   **Broken Alternative:** The app suggests a non-existent or unavailable alternative route, leading to the user being stranded.

### Requirements Wanted
| Requirement | Priority |
| :--- | :--- |
| **Single Point of Truth (Backend)**: Centralized data entry for platform updates to sync app and physical boards. | Critical |
| **Status & Delay Notifications**: Push alerts containing delay time, reason, and platform number. | Critical |
| **Stale Data Management**: Timestamping updates; suppressing push notifications for expired/outdated events. | High |
| **Alternative Route Guidance**: Providing specific alternative train/bus options for cancellations. | High |
| **Human Help Access**: In-app contact/location info for station staff/help desks if digital guidance fails. | Medium |
| **Live Map Integration**: Visualizing train location on a map. | Low |

### Conflicts between Roles
*   **Operational Reality vs. Digital Intent:** The Product Owner acknowledges that platform data is often updated manually and inconsistently by local staff, creating a high risk of "app vs. board" discrepancies.
*   **Feature Prioritization:** The Product Owner is prioritizing the integration of the backend data system over the passenger-facing "live map" feature.

### Open Questions / Assumptions
*   **Assumption:** The technology exists to enforce a "Single Point of Truth" where the station supervisor’s entry simultaneously updates all digital and physical touchpoints.
*   **Assumption:** There is a reliable way to differentiate between a "current" alert and a "stale" alert for users re-entering coverage areas.
*   **Open Question:** How will the system handle alternative routes involving third-party operators (buses/other lines) that are not under SmartRail's direct control?
*   **Open Question:** What is the technical feasibility of suppressing push notifications for offline users vs. just updating the main screen?
