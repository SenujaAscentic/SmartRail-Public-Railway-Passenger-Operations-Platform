# Session station-manager+railway-controller-2026-10-04T10-35-12-516Z

Topic: Who decides platform changes, and how early must they be announced?

**moderator**

Station Manager, when a last-minute platform change is necessary, what is the absolute minimum lead time you need to safely reroute passengers—particularly those with limited mobility—before the train actually arrives?

---

**station-manager**

At a busy interchange like Maradana, we need at least **15 to 20 minutes** of lead time. Our stations rely on overhead footbridges. If a lift is broken—which I often have to post local notices about—moving an elderly grandmother or someone in a wheelchair takes significant time. 

Picture 18:00 on a Friday. The station is absolutely packed with exhausted commuters heading home to Gampaha or Panadura. If the central controller in Colombo Fort suddenly switches a train from Platform 2 to Platform 5 with only five minutes' notice, it is a major safety hazard. You get a literal stampede on the stairs. 

I must have the local authority to reject a platform change if we cannot safely manage the crowd in time. Furthermore, if the network drops during this Friday rush, what happens to our displays? If they show stale data, passengers run the wrong way. The screens must instantly fail-safe to "Refer to Station Staff" rather than displaying incorrect platforms. We on the station floor are the ones who face the angry crowds when things go wrong, not the central controller.

---

**moderator**

Railway Controller, the Station Manager is demanding the authority to reject your platform changes if they are initiated with less than 15 to 20 minutes of lead time. How do you respond to this request for local veto power, and what operational constraints dictate how early you can realistically make and broadcast these platform decisions?

---

**railway-controller**

Veto power? Absolutely not. If Maradana rejects a platform change, the entire Main Line from Colombo Fort to Gampaha grinds to a halt. A train holding outside Maradana blocks the throat, cascading delays across the whole network within minutes. I carry the operational safety and delay-penalty responsibility, not the station.

Let’s talk reality. A 15-minute lead time is a luxury we don't have when a signal fails or a slow commuter train breaks down. Sometimes we only know a platform is blocked when the train is at the home signal, five minutes out. 

The real issue is communication. Today, I have to call the station, they tell a porter, who then updates a board. That lag is what causes stampedes. 

Under SmartRail, my platform change must instantly update the driver's app and the station's digital displays simultaneously. No phone calls, no lag. If a station's network drops, yes, fail-safe to "Refer to Staff." But as long as the system is live, my central entry is the single, non-negotiable source of truth. No local overrides.

---

**moderator**

Station Manager, since the Railway Controller has ruled out local veto power and warns that 5-minute emergency changes are unavoidable, how should the SmartRail system support your team in real-time when a high-risk, short-notice platform change is forced? For instance, what automated alerts or safety workflows must the system trigger on the station floor to prevent a stampede?

---

**station-manager**

If the Controller is going to force a five-minute change on a packed Friday evening, then SmartRail must not bypass my staff. Today, passengers see changes on their phones and start running before my team even knows what is happening!

If we have no veto, we need a **"Staff-First" buffer**. The system must alert station staff devices at least 60 seconds *before* the public displays or passenger apps update. This gives my security and porters a crucial head start to get to the footbridge stairs at Maradana, manage the flow, and prevent a stampede.

Second, the system must link platform changes to our local status logs. If I have flagged that the lift to Platform 5 is broken, the system must automatically trigger a high-priority "Mobility Alert" to our platform staff the moment the change is forced. We must know to immediately dispatch staff to help passengers who cannot use the stairs. 

Finally, we need automated, synchronized audio announcements in Sinhala, Tamil, and English to play instantly over the PA. If the controller forces this, the system must help us manage the chaos on the ground, not accelerate it.

---

**moderator**

Railway Controller, the Station Manager is proposing a mandatory 60-second "Staff-First" delay before platform changes are broadcast to public displays and passenger apps. Given that you are already operating on a tight 5-minute window before arrival, how does holding back passenger information for a full minute impact your dwell times and network flow, especially if the train arrives at the platform while the public is still uninformed?

---

**railway-controller**

A 60-second "Staff-First" delay is an operational risk, but I am willing to concede it *only if* the system forces an immediate, automated PA announcement in all three languages at the moment I hit "Confirm." 

If I have to wait a full minute while the platform is already filling with commuters who don't know the train is moving, we lose control. My priority is the train’s dwell time. If the train sits at the home signal for an extra minute because the platforms aren't clear, we lose the slot. 

However, I agree that a stampede is a safety incident that stops the network anyway. If the system provides that 60-second buffer, it must be used for active crowd management, not just reading a screen. 

My requirement: If the system initiates a change, the "Staff-First" notification must be a **high-priority alert** on their devices—vibration, audio, the works. If they don't acknowledge the receipt of that alert within 15 seconds, the system must trigger a broadcast to the public anyway. We cannot let a lack of staff response turn a 5-minute delay into a 20-minute network failure.
