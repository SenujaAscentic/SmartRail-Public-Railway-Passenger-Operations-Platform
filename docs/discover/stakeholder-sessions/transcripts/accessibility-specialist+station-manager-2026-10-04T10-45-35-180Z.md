# Session accessibility-specialist+station-manager-2026-10-04T10-45-35-180Z

Topic: How are lift outages, assistance requests and audio and visual information handled at the station?

**moderator**

**accessibility-specialist**: When a lift is reported as out of service, what is the specific mechanism for notifying passengers who rely on visual or audio information, and how do we ensure that alternative navigation assistance is provided in real-time?

---

**accessibility-specialist**

A simple "out of order" sign on a screen is not enough. If I am at Colombo Fort and the lift to my platform is broken, I need to know *before* I commit to the station, not when I’m staring at a blocked lift shaft.

The brief is silent on this, but we need a "Step-Free Status" API that feeds into everything: the mobile app, station displays, and even the PA system. If a lift breaks, the system must automatically trigger an alert for station staff to deploy assistance—like a ramp or a staff-escorted alternative route. 

What happens when the "accessible path" is broken? We need a clear, plain-language, multi-modal notification. If I’m a visually impaired passenger, I need that audio announcement to be clear and immediate, not buried in a long list of general updates. And for those of us who don't have a smartphone, there must be a physical, staffed help-point where an operator can give me a viable alternative route. 

Accessibility isn’t a final checklist item; it’s the backbone of the journey. If the station isn't accessible, the journey doesn't exist for me.

---

**moderator**

MODERATOR: **station-manager**, following up on the requirement for real-time assistance, what is the current protocol for station staff to verify and report a lift failure, and how do you ensure that staff are physically available to deploy manual alternatives (like ramps) during peak hours when the station is already at capacity?

---

**station-manager**

Currently, the protocol is a mess. If a lift breaks, a passenger finds me—or my staff—to complain. We then radio the control room, who might eventually update a board if they aren't busy with signal failures or platform changes. It’s reactive, not proactive.

During a Friday evening rush at Colombo Fort, my staff are already stretched thin managing platform crowds and ticket barriers. If a lift goes down, I don't have a "spare" person to act as a porter. I need the system to push an alert to my staff’s handhelds the *second* the sensor trips, so we can redirect people before they get stuck in the crush.

The brief mentions "operational notifications," but it’s silent on **local authority**. I need the power to push a "Lift Out of Service - Use Ramp at Platform 3" notice to every screen in my station *immediately*, without waiting for head office approval. 

If the network drops, those screens must show a "cached" or "local" status, not just go blank or show stale, lying data. If I can't trust the display, I have to use the megaphone, and that’s how chaos starts.

---

**moderator**

**accessibility-specialist**, the Station Manager has identified a critical dependency on staff availability for manual intervention (like ramps) during peak hours. If the "Step-Free Status" API indicates a lift failure but the station is at maximum capacity, what are the minimum accessibility requirements for these manual alternatives to ensure they are safe and dignified, rather than just a "stopgap" that creates new hazards?

---

**accessibility-specialist**

Dignity is not negotiable. If you are relying on manual ramps during a peak-hour crush at a station like Colombo Fort, you are not just "accommodating" me; you are putting my safety at risk in a sea of commuters.

A "stopgap" manual ramp is often a hazard—it creates bottlenecks and invites accidents in crowded conditions. If a lift is down, the manual alternative must be a **pre-planned, staff-escorted route** that avoids the worst of the crowd, not just a ramp dumped in the middle of a platform.

The system must mandate:
1. **Clear, non-visual wayfinding:** Staff must be alerted to my presence and needs, not the other way around. I shouldn't have to search for a porter.
2. **Prioritised boarding:** If the accessible route is compromised, the system must allow me to board in a way that doesn't involve fighting through a crowd.
3. **Information parity:** If the digital system says a lift is broken, the staff’s handhelds must show them exactly where to meet me. 

We need a protocol that triggers *before* the station hits capacity. If the lift is broken, the station's capacity management must reflect that.

---

**moderator**

**station-manager**, given the accessibility-specialist's requirement for pre-planned, staff-escorted routes, how do you currently manage the handover of information between the station staff who report a lift failure and the staff who are responsible for navigating a passenger through these alternative, often non-public, routes?

---

**station-manager**

The handover is currently non-existent. It’s entirely reliant on verbal communication—radio calls or someone shouting across a busy platform. It’s unprofessional, prone to error, and frankly, it’s humiliating for the passenger. 

If a lift fails, my staff member at the lift might know, but the staff at the ticket barrier or the platform edge has no idea. By the time the passenger reaches the platform, they’ve already navigated the hard part, only to find they’re stuck. 

I need a system that links the "Incident" to a "Task." When that lift sensor trips, the system shouldn't just flash a warning; it should create a task on the handhelds of my available floor staff. It needs to show: *Passenger requires escort from Entrance A to Platform 4*. 

The brief is completely missing the **Staff Coordination Workflow**. We are not just monitors; we are responders. If the system knows the lift is out, it should automatically alert the nearest available staff member to initiate the escort protocol. If we’re at peak capacity, I need the system to tell me who is free, otherwise, we’re just guessing and leaving passengers stranded. 

Who authorizes the route change? I do. I need that local override.
