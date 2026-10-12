code .# Discovery Findings

How requirement discovery was run, what it found, and what changed because of it. Companion documents: [00-brief-analysis.md](00-brief-analysis.md), [assumptions-log.md](assumptions-log.md), [conflicts-and-ambiguities.md](conflicts-and-ambiguities.md), [../ai-usage-log.md](../ai-usage-log.md).

## 1. Approach

Section 22 of the brief suggests simulating discussions with stakeholder roles. I built a small LangGraph (TypeScript) tool in `agents/` that runs a moderated discussion between AI personas and saves the transcript.

| Part | Decision |
|------|----------|
| Personas | 11, in `personas/`: daily commuter, occasional commuter (the brief's single "passenger" split in two), driver, station manager, railway controller, customer service, catering provider, security engineer, QA engineer, accessibility specialist, product owner. I edited three of the AI drafts (accessibility specialist, catering provider, daily commuter). |
| Local grounding | A shared Sri Lankan context file for all personas. The daily commuter's frustrations were checked against the app passengers use today (RDMNS: crowdsourced updates, no platforms, notifications only when the app is open). |
| Plan | 22 sessions in `agents/session-plan.json`: pairs with a real working relationship, plus three group scenarios. Not all 55 possible pairs: most pairs share no process and would produce filler. |
| Format | A moderator asks one probing question at a time and the personas answer in character. Each session writes a transcript and a summary to each role's session file. |
| Models | Several Gemini models, with automatic fallback when one hit its free-tier quota or returned an overload error. |
| Review | Every group of transcripts was read and checked against my own brief analysis before anything was accepted. Accepted and rejected items are recorded, with the AI's role, in the AI usage log (entries 27 to 47). |

## 2. Results at a glance

- 22 sessions run, 5 consolidation groups reviewed.
- 12 conflicts recorded (C-1 to C-12): 9 decided or resolved, 2 already decided by earlier assumptions, 1 deferred.
- 27 new assumptions (A-112 to A-138): 22 confirmed, 5 proposed (A-112 and A-117 to A-120).
- Five stale or inconsistent entries in my own notes found and fixed through the checks (the Section 13 delay rule, the station manager role row, QA flow 4, and two rows of the propagation matrix), plus one column added to that matrix.

## 3. What discovery found, by theme

| Theme | Finding | Result |
|-------|---------|--------|
| Authority | Station staff and the controller disagree about who changes platforms. | C-1: the controller owns train movement and platform assignment. Stations post local notices only and send station information to the controller (A-115). |
| Platform changes with little notice | Panic comes from fear of missing the train. | C-2 (my idea, with a condition added in review): hold the train and announce calmly that it will wait, promised only when the controller has set the hold (A-112 to A-114). |
| Delay information | Commuters want early warning; the controller fears false alarms. | C-3: no unconfirmed status. Lateness comes from GPS as exact minutes. |
| Crew reporting | Drivers cannot use a screen while moving. | C-5: fixed incident types for the driver, free text only from the guard (A-116). Controller messages need acknowledgement when stationary (A-134). |
| Passengers | Regular commuters want speed; newcomers want guidance. | C-6: a guest can follow one journey without an account or push alerts (A-121). Notifications state minutes late, reason and platform (A-117). Expired pushes are not sent (A-118). |
| Accessibility | Specialists asked for lift status, step-free routing and escorts. | C-7: lifts are a minor issue and are out of the MVP. WCAG 2.2 AA stays (A-100). |
| Catering | Handover location, payment liability, personal data, and event reliability. | C-8 to C-10: provider staff at designated places with details given to the station beforehand (A-128); pay on collection (no refunds); first name and phone shared with consent. A-122 to A-127: versioned events, a pull endpoint, delivery targets, signed messages, append-only audit, liability as a commercial matter. |
| Security and QA | Lost devices, spare tablets, invalid updates, idempotency, test coverage. | C-11 and C-12, and A-129 to A-137. |
| Disruption walk-through | A rush-hour cancellation exposed that station managers were not alerted. | A-138, and the propagation matrix corrected. |

## 4. Where I rejected AI output

Rejections are as valuable as acceptances. The most important:

- **Out-of-scope features.** The QA and product owner personas spent a whole session on ticket purchase, seat holds and payment reversal. Customer service personas asked for refunds, vouchers and a flag for passengers with accessibility needs. All rejected: no ticketing or payment in the MVP, and SmartRail should not collect accessibility data.
- **Suggestions that contradicted the assessment.** The product owner persona twice proposed deferring catering. The assessment requires the integration.
- **Suggestions that contradicted my decisions.** The station manager entering platforms (C-1), "Delay Likely" statuses (C-3), a 60-second staff-first window (deferred), COD being "off the table" (A-84).
- **Invented infrastructure and facts.** Lift sensors, gate validation scans, tokenised ticket history, a UK setting ("Croydon"), and unverified train names and times. Real Sri Lankan details from the personas were not used as facts.
- **Over-engineering.** Device management, secure-enclave signing, mTLS as "non-negotiable", cryptographic proof of train timing.
- **Misreading the brief.** Agents claimed the brief "completely leaves out" offline use, which Section 16 covers. A rule was added to check the brief before claiming a gap.

## 5. Weaknesses of the method

1. **Personas echo their instructions and give in quickly.** Two agents agreeing is not stakeholder consensus, so no agreement between personas was counted as a decision.
2. **Bias from my own analysis.** The first commuter run read my decisions and repeated them. I removed my analysis from the agents' context, and the rerun produced different, more independent needs. The first transcript is kept as evidence.
3. **Hallucinated detail.** Personas fill gaps with plausible but unchecked facts. The shared context file is an AI-written starting point and still needs checking against an authoritative source.
4. **Short discussions.** Most sessions have only 4 turns, so they surface issues but do not resolve them. Decisions came from my review.
5. **Reproducibility.** The model that answered each turn is not recorded because of the automatic fallback, and free-tier quotas limited how many sessions could run per day.
6. **Missing voices.** Real stakeholders were not interviewed. Station-level staff such as porters, a regional supervisor and a safety officer were not simulated.

## 6. What changed in my requirements

- Roles and responsibilities were sharpened: the station manager's role row and QA flow 4 now say the controller changes platforms.
- The notification model gained content rules (A-117), an expiry rule (A-118) and station manager alerts (A-138). The delay push rule follows A-30 everywhere.
- The catering integration gained concrete reliability rules (A-122 to A-126) and a handover rule (A-128).
- The crew app gained sign-in, device-loss and acknowledgement rules (A-129, A-130, A-134).
- New items were added to the deferred list: customer-service data access, crew device management, step-free routing and escort tasks, a staff-first window, and a legal hold on records.

## 7. Still open

- A-112 (the three lead-time levels) and A-117 to A-120 are Proposed and need confirming.
- C-4 is deferred with customer service.
- Station and line names in the persona context still need verifying (A-20 and S4-11).
- Sri Lanka's Personal Data Protection Act: applicability still to be checked (Section 20).
