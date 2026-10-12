# SmartRail: handoff for Claude chat

Paste this whole file into a new Claude chat. You (Claude) have no access to my files, so everything you need is below. Where a file is named, it lives in my project folder; ask me to paste it if you need its exact text.

## Who I am and what this is
I am doing an individual cross-practice, AI-assisted software development assessment. The brief is "SmartRail", a fictional national railway platform for Sri Lanka (real stations, fictional operator and timetable). It needs: a passenger mobile app, a driver/crew app, station displays and terminals, a railway operations web app, a backend, a simulated external catering integration, QA, security and an AI-usage record. The assessment values requirement discovery, scope management, my own judgment, and showing where I challenged or rejected AI output. I must be able to explain every decision, so please help me think, do not just decide for me.

## How I like to work
- Section by section. You draft; I accept, change or reject; only then does anything get written.
- Be concise and honest. Flag uncertainty. Check numbers before stating them.
- Track everything: every AI interaction goes in my AI usage log (what was accepted, what was rejected and why).
- Agreement between AI personas is not stakeholder agreement. Only my decisions count.

## What I have built so far

### 1. Requirement analysis (my own, section by section)
`00-brief-analysis.md` covers brief sections 2 to 20: what is stated, implied and missing, with IDs (S3-12 and so on), my decisions and open questions. the assumptions log inside `00-brief-analysis.md` holds numbered assumptions (A-01 to A-138) with a status (Proposed, Confirmed, Open) and a deferred table.

### 2. AI stakeholder simulation tool
A small LangGraph (TypeScript) project in `agents/`, using Gemini with automatic model fallback (free-tier quotas are about 20 requests per model per day). It runs moderated discussions between AI personas and saves transcripts.
- 11 personas in `docs/discover/personas/`: daily commuter, occasional commuter, driver, station manager, railway controller, customer service, catering provider, security engineer, QA engineer, accessibility specialist, product owner. Plus a shared Sri Lankan context file. I edited three AI drafts.
- The agents read only the original brief, not my analysis, because my own decisions biased the first run.
- 22 sessions ran (pairs with real working relationships, plus three group scenarios). Transcripts are in `stakeholder-sessions/transcripts/`.

### 3. Consolidation (done)
I reviewed every transcript against my analysis and recorded outcomes in `discovery-findings.md`, `conflicts-and-ambiguities.md` (C-1 to C-12) and the assumptions log. Many agent suggestions were rejected (see below).

## My key decisions so far
**Scope and data**
- MVP uses a small seeded network (2 to 3 lines, 15 to 20 stations), real station names, fictional operator.
- Fares: a fixed price per station pair and class (1st, 2nd, 3rd), display only. No ticketing, no seat booking, no payments in the MVP.
- English only in the MVP, built ready for translation.

**Live train information**
- No signalling feed. Train position comes from the driver app GPS (simulated in the MVP; on-train GPS unit later). No passenger GPS.
- Priority of truth: controller, then driver buttons, then GPS.
- Lateness is shown as exact minutes against the schedule ("late by 3 min"), never an "unconfirmed" status. Data is stale after 2 minutes and "location unavailable" after 10.

**Passenger app**
- Public use needs no account. An account is needed for saved journeys, alerts and food orders. A guest can follow one journey on their device for the day without push alerts.
- Live tracking has two modes (waiting, on board). The passenger taps "I'm on this train" to switch.
- Journey search shows direct trains only. Notifications state minutes late, reason and platform. Out-of-date pushes are not sent.

**Authority and disruptions**
- The controller owns train movement and platform assignment. Station staff post local notices only and send information to the controller (C-1). Station managers are alerted at the same moment as the public for cancellations, early terminations and platform changes.
- Platform changes with under 5 minutes' notice: the controller holds the train and the announcement says calmly that the train will wait, promised only when the hold is actually set (C-2, my idea).
- A 60-second staff-first window was deferred.

**Crew app**
- The driver's view is glance-only. Drivers pick incidents from a fixed list. Only the guard can add free text. Controller messages need acknowledgement, and the driver acknowledges only when stationary. Actions queue offline with timestamps. Crew sign in with their own account on any registered device, and an admin can disable a lost device.

**Catering (external, simulated)**
- The provider owns menu, prices, availability, orders and payment. SmartRail owns train data, accounts and the order-to-journey link.
- Pay on collection in the MVP. Cut-off = live expected arrival minus the provider's lead time for that station. Signed, versioned events both ways with retries and a pull endpoint. No-show strikes (2 suspend ordering for 30 days), item and value limit per order.
- Provider staff at stations must be registered with the station beforehand. Liability for prepared food after a disruption is a commercial matter, outside the MVP.
- Undecided: simulated provider as an independent small app or a mock.

**Other**
- Ops web roles: chief controller, regional controller, station manager, scheduler, system administrator. Two-factor for staff. Customer service is deferred.
- Lifts and step-free routing are out of scope. WCAG 2.2 AA is the accessibility target. One shared design system.
- QA: 5 automated end-to-end flows, controllable clock in tests. Threat model planned for 11 Oct. Data classes and retention (trip history 90 days).

## Where I rejected AI output (important for the assessment)
- Ticket purchase, seat holds and payment reversals (out of scope).
- A persona suggesting catering be deferred (the brief requires it).
- "Delay Likely" unconfirmed statuses and a local emergency override for stations.
- Refunds, vouchers, flags for passengers with access needs.
- Invented infrastructure (lift sensors, gate scans) and a UK setting in one persona.
- Over-engineering (device management, mTLS as mandatory, cryptographic proof of train timing).

## Limits you should know about
- Personas are AI and give in quickly. Some Sri Lankan details (station and line names, train types) are AI-written and not yet verified against an authoritative source.
- Still Proposed and awaiting my confirmation: A-112 (three lead-time levels, never listed) and A-117 to A-120.
- Open: Sri Lanka's Personal Data Protection Act applicability, and whether the model used per transcript turn should be recorded.
- IMPORTANT numbering note: my local assumption log is at A-138. Any other copy you have seen may stop earlier (one stopped at A-114). Number new assumptions from **A-139**, and ask me to paste the current log if you need it.

## What I might ask you for next
The assessment still needs: personas and journeys, user stories with acceptance criteria and edge cases, user flows and wireframes, a design system, architecture and decisions, backend, web, mobile and crew apps, a station experience prototype, the catering simulator, a QA strategy and test evidence, a threat model, and my final demo story. Likely next topic: choosing what comes first and a plan for building the MVP.
