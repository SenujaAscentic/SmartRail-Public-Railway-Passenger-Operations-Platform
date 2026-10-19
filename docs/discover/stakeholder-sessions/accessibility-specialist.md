# Accessibility Specialist

## 2026-10-04T10:43:43.529Z - What does a station and journey need so that newcomers and passengers with access needs can navigate?

Roles: occasional-commuter, accessibility-specialist
Transcript: transcripts/occasional-commuter+accessibility-specialist-2026-10-04T10-43-43-528Z.md

Here is the business analysis based strictly on the provided transcript.

---

### Needs

*   **Step-by-Step Navigation:** Simple, step-by-step guidance from the station entrance directly to the correct platform.
*   **Clear Train Information:** Plain-language signage and digital boards indicating whether a train is express or slow, its destination (e.g., Galle), and which smaller stations it will skip, rather than just train names or abbreviations (e.g., "Ruhunu Kumari" or "EXP").
*   **On-Platform Verification:** Real-time reassurance on the platform confirming the identity of the train currently pulling in (e.g., verifying if it is the 3:30 PM to Kandy or an overdue slow train).
*   **Frictionless Access:** Ability to check schedules, 2nd class fares, and track a specific journey (e.g., "Fort to Kandy") as a guest without creating an account, registering, or logging in.
*   **Quick Station Departure Info:** A tap-or-scan poster at station entrances to instantly load the day's live departures on a mobile device.
*   **Multimodal Platform Change Alerts:** Simultaneous, synchronized updates across station displays, terminals, and audio announcements in plain Sinhala, Tamil, and English. Audio announcements must be converted to high-contrast visible text on screens.
*   **Haptic and Accessible Mobile Alerts:** Mobile notifications using distinct vibration patterns and screen-reader accessible text (never relying on color alone to signal changes).
*   **Accessible Routing:** Step-free routing instructions during platform changes that verify lift operational status before redirecting passengers.
*   **Staff Assistance Alerts:** Immediate notifications sent to station staff to assist passengers when a lift on a routed path is broken.
*   **Automatic Accessibility Inheritance:** Mobile application must automatically inherit standard OS accessibility settings (such as screen readers or high-contrast text) in guest mode.
*   **One-Tap Accessibility Filters:** A prominent "Step-Free Routing" toggle on the very first screen of the guest interface to filter searches for stations with working lifts and ramp-accessible platforms.
*   **Accessible Physical Terminals:** Public terminals equipped with physical, tactile buttons positioned under 1.2 meters high that instantly trigger audio-guided step-free routes or switch the display to high-contrast Sinhala, Tamil, or English.

---

### Pain Points

*   **Navigation Anxiety:** Passengers feeling lost, overwhelmed, and disoriented when entering large, noisy stations (e.g., Colombo Fort, Maradana) with luggage.
*   **Delayed Platform Information:** Platform assignments are often unknown until minutes before departure, forcing passengers to haul heavy bags up and down footbridges.
*   **Inaudible Announcements:** Crackly station speaker announcements that are highly difficult to hear or understand.
*   **Confusing Signage:** Display boards using abbreviations and train names instead of clearly explaining the train type and stops.
*   **Risk of Boarding the Wrong Train:** Lack of confirmation on the platform leads to fear of boarding an express train by mistake and missing the intended stop.
*   **Forced Registration Barriers:** System forcing users to undergo email verification and password creation while rushing or carrying luggage, which acts as a major barrier—especially for elderly passengers with shaky hands or visually impaired users.
*   **Inaccessible Platform Changes:** Last-minute track changes (e.g., from Platform 1 to 4) announced via audio are impossible to navigate quickly for wheelchair users who cannot use footbridges.
*   **Deficiencies in the Current Brief:** The current brief completely ignores step-free routing, lift outage tracking, screen-reader standards, and multi-language accessibility.

---

### Failure Scenarios

*   **Missed Trains Due to Forced Logins:** A passenger rushing to check if a train is express or slow misses their train entirely because they are forced to spend three minutes creating and verifying an email account.
*   **Stranded Wheelchair Passengers:** A passenger in a wheelchair is routed to a platform via a path with a broken lift or stairs, leaving them stranded because the system failed to verify lift status before sending the update.
*   **Missed Destination Stops:** A passenger boards an express train by mistake and misses their station because the signage only listed the train's name/abbreviation rather than explicitly stating it skips smaller stations.
*   **Undetected Platform Changes:** A visually impaired or deaf passenger misses a last-minute platform change because the update was communicated using color-only visual cues or audio-only announcements.

---

### Requirements Wanted (with Priority)

#### High Priority
*   **Guest Mode Access:** Ensure all basic functions (searching schedules, checking 2nd class fares, and tracking a single journey for the afternoon) are fully functional without requiring user registration or login.
*   **Step-Free Routing Toggle:** Implement a one-tap "Step-Free Routing" toggle on the initial guest screen that filters journeys to stations with working lifts and ramp-accessible platforms.
*   **OS Accessibility Inheritance:** Ensure the app automatically inherits mobile OS settings (screen readers, high-contrast text) without requiring an account.
*   **Synchronized Multimodal Alerts:** Synchronize visual, audio, and mobile alerts for platform changes simultaneously in Sinhala, Tamil, and English. Convert audio to high-contrast text on station screens.
*   **Dynamic Lift Status Verification:** Integrate lift status tracking into the routing engine so passengers requiring step-free access are never routed to platforms with broken lifts.

#### Medium Priority
*   **Station Entrance QR/NFC Posters:** Provide physical posters at station entrances that passengers can tap or scan to instantly load live departures.
*   **Tactile Public Terminals:** Install physical, tactile buttons on station terminals at a height below 1.2 meters to trigger audio-guided step-free routes and high-contrast multilingual displays.
*   **Station Staff Alert System:** Build an automated alert system to immediately notify station staff when a passenger with accessibility needs is routed to a platform where a lift is broken or needs manual assistance.

---

### Conflicts Between Roles

*   **Account Registration vs. Guest Mode:** The system brief outlines that passenger functionality may require creating an account. However, both the Occasional-Commuter and the Accessibility-Specialist strongly conflict with this design, stating that forcing a login creates an unnecessary digital checkpoint, causes passengers to miss trains, and serves as a major barrier to accessibility for elderly and disabled users. They demand a fully capable anonymous "guest mode."

---

### Open Questions / Assumptions

*   **Assumption:** The system will have a reliable, real-time mechanism to track and verify the operational status of station lifts and ramps.
*   **Assumption:** Station staff will be available and positioned to respond immediately when the system triggers an alert for manual assistance at a broken lift.
*   **Open Question:** How will the system deliver real-time, location-specific platform alerts to a guest user's phone if they do not have an account or stored profile?
*   **Open Question:** How will the system physically integrate and maintain the tactile buttons (under 1.2 meters) on the public terminals?

## 2026-10-04T10:45:35.182Z - How are lift outages, assistance requests and audio and visual information handled at the station?

Roles: accessibility-specialist, station-manager
Transcript: transcripts/accessibility-specialist+station-manager-2026-10-04T10-45-35-180Z.md

### Needs
*   **Step-Free Status API:** A system to feed real-time lift status to mobile apps, station displays, and PA systems.
*   **Proactive Alerts:** Automated notifications triggered by sensors directly to staff handhelds.
*   **Local Authority/Override:** Station managers need the power to push status updates and route changes to displays immediately without head office approval.
*   **Unified Task Management:** A system that translates a lift failure into specific tasks for staff (e.g., "escort passenger from Entrance A to Platform 4").
*   **Data Integrity:** Reliable display information that defaults to cached/local status if the network drops.
*   **Staff Coordination:** A workflow that ensures information is shared across all staff members (barriers, platforms, and control room).

### Pain Points
*   **Reactive Protocols:** Current processes rely on passenger complaints to identify issues rather than automated alerts.
*   **Information Silos:** Staff at different locations (e.g., ticket barriers vs. platforms) are unaware of lift failures, leading to "humiliating" experiences for passengers.
*   **Communication Gaps:** Dependence on verbal radio calls or shouting, which is error-prone and unprofessional.
*   **Resource Constraints:** Staff are already stretched thin during peak hours; they lack the capacity to act as porters without a structured, alerted workflow.
*   **Hazardous Alternatives:** Manual ramps in crowded areas create bottlenecks and safety risks.

### Failure Scenarios
*   **Network Failure:** Current displays may go blank or show "stale, lying data" if the connection is lost.
*   **Communication Breakdown:** Staff at the platform are unaware of a lift failure, causing passengers to travel through the station only to find they are stuck at the lift shaft.
*   **Crowd Crush:** Relying on manual ramps during peak hours creates physical hazards for passengers in a sea of commuters.
*   **Information Asymmetry:** The passenger is forced to search for staff to report needs, rather than staff being alerted to the passenger's presence.

### Requirements Wanted
| Requirement | Priority |
| :--- | :--- |
| **Step-Free Status API** (Integration with apps/PA/displays) | High |
| **Automated Staff Handheld Alerts** (Triggered by sensors) | High |
| **Local Override/Control** (Station manager power to update displays) | High |
| **Task Management System** (Linking incidents to specific staff tasks) | High |
| **Non-visual/Multi-modal Notifications** (Clear, immediate audio) | High |
| **Physical Staffed Help-Point** (For non-smartphone users) | Medium |
| **Cached/Local Display Data** (To prevent blank/stale screens) | Medium |

### Conflicts Between Roles
*   **Responsibility vs. Capacity:** The Accessibility Specialist expects staff-escorted routes, while the Station Manager reports that staff are already at capacity during peak hours and cannot spare personnel for manual portering without a more efficient, system-led workflow.
*   **Autonomy vs. Protocol:** The Station Manager requires the authority to make immediate, local route changes, which conflicts with current centralized, slow bureaucratic processes.

### Open Questions / Assumptions
*   **Assumption:** The station has the technical infrastructure (sensors) to trigger alerts, though the current software protocol is "a mess."
*   **Open Question:** If the station is at absolute maximum capacity, how does the system balance the "pre-planned route" requirement with physical space limitations?
*   **Open Question:** Who specifically is responsible for the "non-public" routes mentioned, and are these routes mapped out for staff access?
*   **Assumption:** The current "brief" provided to the team is incomplete regarding local authority and staff coordination workflows.
