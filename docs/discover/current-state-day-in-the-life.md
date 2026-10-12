# Current State: A Day and a Week in the Life (Before SmartRail)

How the main roles work today, hour by hour and week by week. This is the "as-is" picture that SmartRail replaces or supports.

**Read this first.** Every fact below comes from AI personas answering a chronological interview (sessions `d1` to `d5` in `agents/day-in-the-life-plan.json`; transcripts in `stakeholder-sessions/transcripts/`). Times, frequencies, equipment and station details are **simulated and unverified**. Treat them as hypotheses to check with real passengers and railway staff, or against Sri Lanka Railways sources, before relying on them.

## 1. Daily commuter (passenger)

| When | What happens today |
|------|--------------------|
| 06:15 | Leaves home in Gampaha. While drinking tea opens the RDMNS app and taps the saved route to Colombo Fort for the 06:40 train. Often sees a grey box because data has not refreshed or signal is weak. |
| 06:15 to 06:40 | Takes a bus to the station. If the app hangs, puts the phone away and hopes. |
| At the station | RDMNS shows no platforms. Reads the main notice board, listens to the PA (often garbled), watches the crowd and asks other regulars or the guard. |
| About 17:00 | Leaves the office and goes straight to the big concourse boards at Colombo Fort for the train home. Asks a station official if the board only says "Delayed". Only then opens RDMNS to read passenger reports. |

**Week:** Weekday routine is fixed. On Saturdays and Poya days the schedule differs, the commuter leaves for the station earlier than needed, relies on the station master's loudspeaker, and finds RDMNS thin because few regulars post updates.
**Tools and sources:** RDMNS (crowdsourced), station boards, PA, guard, fellow commuters, phone calls to family and manager.
**What goes wrong:** An "indefinite delay" (signal failure or breakdown, no information) at least twice a week. Result: waiting on the platform or train, phoning family and the manager, refreshing RDMNS.

## 2. Occasional commuter (passenger)

| When | What happens today |
|------|--------------------|
| A day or two before | Asks friends which train is the "good" one. Does not trust online schedules and avoids apps that feel built for engineers. |
| 45 to 60 minutes before departure | Arrives at Colombo Fort early. Reads the display boards (small, flickering, abbreviations), shows the ticket to a uniformed staff member, asks other passengers. |
| On arrival | Stops on stepping off the train, lets the crowd pass, looks for exit signs or a station master's office. Worried about the wrong exit, especially after dark. |

**Week and pattern:** Travels a few times a month. Avoids rush hours. Weekends and public holidays are calmer but harder to predict (special timetables, many families with luggage).
**Tools and sources:** Friends, board, staff, other passengers. No app.
**What goes wrong:** Delay or cancellation about once every three or four trips. No alerts, so waits at the board, hunts for staff, sometimes waits an hour or more, or takes a slow train.

## 3. Train driver

| When | What happens today |
|------|--------------------|
| 04:30 | Starts at the Maradana running shed. Signs the logbook, collects the paper "driver's pack" (temporary speed restrictions, track works, signal failures on the Main Line), checks watch against the station clock, takes a radio. |
| Before departure | Walk-around of the locomotive (brake pipe pressure, headlights, sanders). Reports radio faults to the shed foreman. |
| First hour | Departs on the guard's or platform staff's "Right Away". Eyes on signals. Compares paper circulars against trackside markers. Speaks on the radio only when held at a red signal. |
| End of shift | Fills in the logbook (arrival time, engine number, mileage, delays with reasons), signs the Defect Book, returns radio and papers, staples any written instruction from the controller, foreman signs off, clocks out. |

**Week:** Weekdays: fast commuter services (for example Veyangoda and Panadura). Weekends: fewer commuter and more intercity trains, heavier and fuller, and more "caution" orders because track work is scheduled then.
**Tools and sources:** Paper circulars, logbook, defect book, radio, signals, emergency trackside telephone, the signalman and controller. No phone or screen in the cab.
**What goes wrong:** Signal failure or communication breakdown two or three times a week, worse in the monsoon. Stops, radios the signalman with train number, location and signal number, waits for verbal or written "Caution Order", records it.

## 4. Station manager

| When | What happens today |
|------|--------------------|
| 06:00 | Walks the platforms and checks the boards against the printed timetable. Reads the station logbook for overnight problems. Tunes the radio to the control frequency. Checks the RDMNS app on a personal phone to see what passengers are saying. |
| 07:00 to 09:00 | Peak. Platform staff report crowd levels by handheld radio ("Platform 2 at capacity, hold entry at the barriers"). Staff also flag boards that are wrong. The manager decides whether to override the PA. |
| 14:00 | Hands over face to face in the station master's office using the bound station logbook (platform alterations, incidents, equipment failures). Swaps radio battery, passes on verbal warnings. No digital handover. |

**Week:** Weekdays: commuter volume and safety. Weekends: fewer staff, more tourists asking questions, harder to help passengers when a lift fails, and logbook entries are shorter.
**Tools and sources:** Radio, personal mobile, station logbook, printed timetable, PA microphone, the control room desk, platform staff, RDMNS.
**What goes wrong:** An unannounced or late platform change for a Main Line express, three or four times a week. The crowd surges toward the wrong platform. The manager takes the PA microphone, announces in Sinhala, Tamil and English, and radios staff to block the stairs or platform edge, within about the first 60 seconds.

## 5. Railway controller

| When | What happens today |
|------|--------------------|
| 05:45 | Clocks in. Reads the logbook, then briefs with the outgoing controller on the "hot list": faulty signals between Fort and Maradana, short-formed intercity trains, speed restrictions, any failed engine. Checks radio logs and night-shift notes. Takes the desk after about five minutes. |
| First decision | Platform allocation for the incoming commuter rush at Colombo Fort. Uses the physical track diagram board with section-occupancy lights, cross-checked with the signalman's radio report, because digital screens can show a train as arrived too early. |
| 14:00 | Hands over by physically giving the next controller the "Daily Occurrence Book" (every delay over ten minutes, signal failure and unscheduled stop, written in pen) and a shift report on long-distance trains, then walks through live issues at the wall diagram. |

**Week:** Weekdays: throughput of commuters into Fort. Weekends: leisure and long-distance traffic, longer dwell times, and a "Rolling Stock Audit" to confirm first and second class carriages are attached to intercity trains.
**Tools and sources:** Logbook, Daily Occurrence Book, wall track diagram, radio, telephone, the signalman, station masters, drivers. Digital screens are not trusted.
**What goes wrong:** A stalled commuter train (passenger alarm or door obstruction, mostly on the Coastal Line at peak), three or four times a week. The driver radios, the controller sends a guard or technician through the nearest station master, freezes following traffic, decides on a bypass or relief engine, and phones the next three major stations for delays over 15 minutes.

## What the five days have in common

- **Information moves by voice and paper.** Radio, phone, shouting, handwritten logbooks (station logbook, Daily Occurrence Book, driver's pack and defect book). Nothing is shared digitally between roles.
- **Everyone learns about problems late.** Passengers learn from the crowd, station staff from passengers and RDMNS, and drivers from the signalman. Station and control staff use the unofficial RDMNS app to see what passengers know.
- **Weekends are not quiet, just different.** Fewer staff, tourists, track works, special Poya timetables.
- **Handover is a ritual done with paper.** Controller and station manager both finish at 14:00 with a physical logbook handover.

## Points worth taking into your analysis

1. **Shift handover** is an open process in Section 9 of `00-brief-analysis.md`. Today it is a paper logbook; this is evidence of what a digital handover would replace.
2. **Weekend and Poya timetables** support the service calendar design (A-12, A-13, A-17).
3. **Frequency numbers** (delays, platform changes, signal failures and stalled trains each several times a week, per these personas) can seed realistic scenarios for the simulator and tests, once verified.
4. The controller and driver describe **signalling, section clearance and caution orders**. These stay outside SmartRail (see the proposed A-140 on the information-versus-control boundary; it is still awaiting your decision).
5. **Platform change timing** appears again as the station manager's most frequent problem, which supports C-1, C-2 and A-138.

## Quality problems in these answers

- Personas disagree: platform changes happen "three or four times a week" (station manager) while the controller never mentions them as a top issue.
- The daily commuter lives in Gampaha here but in Veyangoda in earlier sessions, and calls the 06:40 an "express".
- The station manager and the "station master" are used as one role, and the controller does work that real railways split with signalmen. Role boundaries need checking.
- The driver describes a locomotive-hauled train; real commuter fleets may differ.
- No persona is a real person. Numbers are plausible, not measured.
