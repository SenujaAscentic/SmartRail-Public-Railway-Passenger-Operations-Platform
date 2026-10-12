# SmartRail – Edge Cases (MVP)

**Status:** 7 Oct 2026. Consolidated from the brief analysis, stakeholder simulations, journey maps and user stories.
Each edge case names the expected behaviour and the story that covers it. **Test** = covered by an automated test (E2E flows or unit / integration tests).

## 1. Live data and tracking

| # | Edge case | Expected behaviour | Story | Test |
|---|---|---|---|---|
| E-01 | GPS lost in a tunnel | Hold last position; "Last seen" after 2 min; ETA stays stable | US-B4, US-P4 | Unit |
| E-02 | No position for 10 min | "Live location unavailable – scheduled times shown" | US-P4 | Unit |
| E-03 | Crew phone offline, reconnects with old positions | Queued data applied by time it happened; never overwrites newer data | US-C7, US-B3 | Integration |
| E-04 | Position jumps 5 km in 10 s / goes backwards / off route | Rejected as implausible | US-B3 | Unit |
| E-05 | Driver starts the wrong journey | Positions rejected (off route / not assigned); start only for assigned journey | US-C2, US-B3 | Unit |
| E-06 | Train stops at a red signal for 2 min | Delay shows briefly; no alert; no push (< 5 min) | US-P4, US-O8 | Unit |
| E-07 | Train stopped between stations ≥ 3 min | "Unscheduled stop" alert to controller only | US-O8 | Unit |
| E-08 | Delay flickers 6 ↔ 7 min | Displayed value changes only after two consistent updates | US-P4 | Unit |
| E-09 | Train runs early | "2 min early"; predicted departures never earlier than scheduled; actual early departure reported | US-B4 | Unit |
| E-10 | Controller override contradicted by GPS | Warning to controller until kept or cleared | US-O3 | Integration |
| E-11 | Train passes a station without stopping | Recorded as "passed"; platform display warns "does not stop here" | US-B4, US-D2 | Unit |
| E-12 | Overnight run crossing midnight | Stays one journey on its service date | US-B2 | Unit |
| E-42 | Cab tablet is in a different train set from the one assigned to the journey | Driver warned, controller alerted (Should S-11); tracking still attributed to the journey | US-C2 | Integration |
| E-43 | Cab tablet clock is wrong | Position times corrected with GPS time / server clock offset; train never shown early or late because of it | US-B3 | Unit |
| E-44 | Driver forgets to end the journey | Prompt at the final stop; tracking stops when the journey is complete (automatic end is a Could) | US-C2 | Unit |
| E-45 | Driver signs in on a second tablet | Previous cab session ends; only one active session | US-C2 | Integration |
| E-46 | Driver tries to start a journey from a phone | Not allowed; phone mode is read-only for the driver | US-C1 | Unit |

## 2. Disruptions and controller actions

| # | Edge case | Expected behaviour | Story | Test |
|---|---|---|---|---|
| E-13 | Wrong train cancelled by mistake | Reinstate; everyone told about the cancellation is told it runs | US-O4 | Integration |
| E-14 | Two controllers edit the same journey | Second save refused: "Changed by … – reload" | US-O4 | Integration |
| E-15 | Cancelled train still moving (empty to depot) | Its positions not shown to passengers | US-B3 | Unit |
| E-16 | Stop added at a station the train does not pass | Not allowed | US-O4 | Unit |
| E-17 | Platform change under 5 min, controller cannot hold | Announcement makes no "will wait" promise | US-O5 | E2E 4 |
| E-18 | New platform already expected to be occupied | Conflict warning naming the other train | US-O5 | Unit |
| E-19 | Journey crosses from one region to another | *Deferred 9 Oct: one line, no regions in the MVP* | – | – |
| E-20 | Out-of-scope action (station manager on another station; crew on a journey not assigned to them) | Refused and logged | US-O1, US-B7 | Integration |
| E-21 | Crew instruction not acknowledged | Highlighted after 2 min; radio remains the backup | US-O7 | Unit |

## 3. Passenger app

| # | Edge case | Expected behaviour | Story | Test |
|---|---|---|---|---|
| E-22 | No direct train | "No direct trains" ("Change at" deferred with the second line, 9 Oct) | US-P1 | Unit |
| E-23 | Saved train does not run today (Poya day, weekend) | Card says so | US-P6 | Unit |
| E-24 | Passenger forgets to tap "I'm on this train" | "Are you on board?" prompt after departure | US-P3 | Unit |
| E-25 | Passenger missed the train | "No, I missed it" → next trains | US-P3 | Unit |
| E-26 | Push permission refused | Everything still visible in the app | US-P7 | Manual |
| E-27 | Push arrives after the train left the passenger's station | Not sent (expired) | US-P7 | Unit |
| E-28 | Same change detected twice | Only one push | US-P7 | Unit |
| E-29 | Phone offline | Last data with its age | US-P4 | Manual |
| E-30 | No train set assigned yet | "Usually: power set" | US-P2 | Unit |
| E-31 | 5 wrong sign-in attempts | Locked for 15 min | US-P5 | Integration |

## 4. Station displays

| # | Edge case | Expected behaviour | Story | Test |
|---|---|---|---|---|
| E-32 | Display loses connection > 2 min | Banner + scheduled times; trains removed by clock | US-D4 | Integration |
| E-33 | Display reconnects after changes happened | Full state reloaded, not just new events | US-D4 | Integration |
| E-34 | Several announcements at once | Emergency > disruption > general | US-O9, US-D3 | Unit |
| E-35 | Announcement text contains markup or script | Shown as plain text only | US-O9 | Security test |
| E-36 | After the last train of the day | "No more trains today" | US-D1 | Unit |

## 5. Catering integration

| # | Edge case | Expected behaviour | Story | Test |
|---|---|---|---|---|
| E-37 | Provider unreachable | Events retried; "Menu unavailable" in app; train info unaffected; alert after 5 min | US-F2, US-F3, US-F4 | Integration |
| E-38 | Same event delivered twice | Provider ignores the duplicate (event ID) | US-F2 | Integration |
| E-39 | Forged or altered event | Provider rejects it (signature) | US-F2 | Security test |
| E-40 | Hotline outside opening hours | "Hotline closed – opens 06:00" | US-P8 | Unit |
| E-41 | Collection station skipped or closed | Stop-change event to provider within 10 s | US-F2 | E2E 5 variant |

## 6. Deferred edge cases (recorded, not handled in the MVP)
Train splitting / joining · driver change mid-journey · train set swapped mid-day · knock-on delays from train set reuse · full route diversion onto another line · no station manager on duty for a station notice · real PA system failure.
