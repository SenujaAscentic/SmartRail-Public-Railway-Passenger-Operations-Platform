# Session security-engineer+qa-engineer+catering-provider+driver-2026-10-04T10-54-05-319Z

Topic: What are the biggest risks across the apps and the integration, and how would each be caught?

**moderator**

@security-engineer, considering the integration between the passenger-facing app and our backend reservation system, what is your primary concern regarding potential unauthorized data access during the API handshake, and what specific monitoring or validation mechanisms do we need to implement to catch an intrusion attempt?

---

**security-engineer**

My primary concern is **broken object-level authorization (BOLA)** and the lack of proper identity context during the handshake. If a passenger can manipulate a request—say, changing a `passenger_id` or an `order_id` in a URL parameter—they could pull someone else’s journey details or catering order. We see this all the time: developers assume that if someone has a token, they are allowed to see any record. That’s a dangerous mistake.

As for the handshake, I don't want to hear about "public" APIs. Every request must be authenticated, scoped, and rate-limited. If I see a spike in 403 Forbidden errors from a single IP or user account, that’s an automated reconnaissance attempt—we need to flag that immediately.

The brief completely ignores **non-repudiation** for operational commands. If a driver’s app sends a "train position" update, how do we know it’s not a spoofed signal meant to cause panic? We need signed requests, not just encrypted ones. And for the love of everything, keep sensitive PII out of the logs. If I see a passenger's name or phone number in an audit trail, the system is already failing.

---

**moderator**

@qa-engineer, building on the security concerns regarding spoofed operational signals and unauthorized data access, what is your strategy for testing the system's "fail-safe" states—specifically, how will you verify that the app behaves correctly when it receives an invalid or malicious payload during a live train operation?

---

**qa-engineer**

Look, before we talk about "fail-safes," we need to define what a "safe" state actually is. If the system receives a spoofed signal that a train has "arrived" at Maradana when it’s actually near Gampaha, what is the platform supposed to do? Does it auto-update the display? Does it lock the driver out? 

If the requirements don't define the "source of truth" hierarchy, I can’t test it. I need to know: if there’s a conflict between GPS, track sensors, and a manual driver update, which one wins? 

My test strategy for these malicious payloads involves:
1. **Negative Testing:** Injecting malformed data (out-of-bounds coordinates, future timestamps, or invalid train IDs) to see if the system crashes or, worse, blindly updates the public boards.
2. **State Transition Testing:** What happens if we get an "arrived" update without a "departed" update for that station? 
3. **Latency Stress:** How does the system handle an out-of-order sequence of updates? 

If the backend accepts a payload without verifying the *state* of the journey, we aren't testing software; we're testing a disaster. What exactly happens to the passenger display when the data is rejected? Does it show "Delayed," "Unknown," or the last known good state? Give me numbers.

---

**moderator**

@catering-provider, building on the security and QA concerns regarding unauthorized access and data integrity, how does your system handle a scenario where a passenger requests a refund or changes their catering order at the exact moment a train experiences a connectivity loss or a system-wide "fail-safe" state, and how do we ensure the catering team receives the correct, final order status once synchronization is restored?

---

**catering-provider**

Listen, let’s get one thing clear: my kitchen isn't a charity. If a train is delayed or a signal drops, my staff are already prepping, the ingredients are chopped, and the gas is burning. 

When connectivity fails, I don't care about your "system-wide fail-safe" states; I care about my margins. If a passenger tries to cancel or refund an order while the train is in a dead zone, who covers that loss? If your system doesn't provide an **idempotent** way for me to reconcile my order list once the network comes back, I’m going to end up with wasted meals and an angry passenger demanding money I’ve already spent on supplies.

I need a hard cut-off. Once I start prepping, no changes. Period. If your app lets a passenger hit "cancel" after that time, that’s on the railway to pay me, not the passenger. 

Also, stop talking about "synchronization." I need a local cache of my orders. If your central server goes down, my terminal at the station needs to show me exactly what’s due for the next train, regardless of whether you're having an "operational incident." Who is liable for the food if your sync fails?

---

**moderator**

@driver, considering the catastrophic risks of spoofed operational signals and lost connectivity that the security and catering teams just raised, what happens in your cab if the SmartRail app suddenly loses GPS and cellular signal while you are approaching a restricted speed zone, and how do you manually override or confirm your actual train state with central operations without creating a dangerous distraction?

---

**driver**

Listen to me carefully. If my app loses GPS or cellular signal in a cutting or a tunnel—which happens all the time on the Main Line—you know what I do? **Nothing different.** 

My eyes are on the line, my hands are on the controls, and I am watching the physical signals by the track. That screen in the cab is secondary. If some digital system starts glitching or screaming for attention because it lost signal, it’s a dangerous distraction. A device must *never* distract a driver in motion. 

If I need to talk to central operations when the signal drops, I use the radio. I don't tap away at a phone trying to manually override a stuck journey status or type out messages. I talk to the railway controller. I trust those voice instructions over any app, every single day of the week. 

And another thing: don't put anything on that app that looks like an instruction from the controller. If the system drops connection or shows out-of-date timings, I'm driving by the book of rules and the trackside signals, not whatever cached data your app is holding onto. Keep the software out of my line of sight when I'm moving.
