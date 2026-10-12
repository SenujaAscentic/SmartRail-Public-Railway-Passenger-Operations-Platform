# Session product-owner+daily-commuter+occasional-commuter-2026-10-04T10-53-06-848Z

Topic: Which passenger features must be in the first release, and what can wait?

**moderator**

@product-owner: Beyond core ticketing and scheduling, which specific passenger-facing feature is most critical to include in the first release to prevent a significant support burden during the rollout?

---

**product-owner**

Listen, let's be clear: we are bleeding credibility because our passengers are currently relying on third-party apps that don't have our internal data. If we launch an MVP, it *must* solve the "Where is my train?" anxiety.

The most critical feature is **real-time disruption alerts linked to specific train journeys.**

Why? Because when a train is delayed or cancelled—which happens daily on the Coastal or Main lines—our station staff get overwhelmed with the same questions. If we push a notification to the passenger’s phone the moment a delay is logged in our operations system, we cut the "information vacuum" that leads to platform crowding and frustration.

I am deferring the catering integration for this first release. I know it’s in the brief, but we cannot sell food on a train if we cannot reliably tell the passenger where that train is. 

I need the team to rank: Is it more important to show the live map location, or just the status (On-time/Delayed)? We have limited budget, so give me the smallest useful version that stops the support desk from ringing off the hook.

---

**moderator**

@daily-commuter: If you receive a notification that your train is delayed, what specific information do you need immediately to decide whether to wait for that train or find an alternative route, and how does that affect your trust in the app?

---

**daily-commuter**

Look, if I’m standing at the station in the morning, I don’t need a fancy map showing a little train icon moving along a line. That’s just battery drain and data waste.

When I get a notification, I need three things immediately:
1. **The delay time** (e.g., "Delayed by 25 minutes").
2. **The reason** (Is it a signal failure? A technical issue? If I know it’s a major problem, I know to leave the station and catch a bus instead of waiting).
3. **The platform number.**

The platform is the biggest headache. If the app says my train is delayed but doesn't tell me if it’s still arriving on the same platform—or if it's been moved to the other side—I have to run across the footbridge at the last second. 

If your app tells me "Delayed 20 mins, Platform 3," I trust it. If it gives me vague "Service disruption" messages without telling me *my* train's status, I’ll ignore it and go ask the station guard anyway. If the app and the station board don't match, I stop using the app. It's that simple.

---

**moderator**

MODERATOR: @occasional-commuter: Unlike daily commuters, you aren't as familiar with the station layout or alternative routes; if a disruption notification hits your phone while you are en route to the station, what additional information or guidance would you require to avoid becoming stranded or lost at an unfamiliar stop?

---

**occasional-commuter**

I completely agree with the daily commuter that I don’t need a live map—that would just confuse me. But if I’m at a station I don’t know, a simple "20-minute delay" isn't enough.

If my train is delayed or cancelled, I feel panicked because I don't know the station layout. I need:

1. **Clear instructions on what to do next:** If my train is cancelled, tell me exactly which *other* train I can take to get to my destination. Don’t just tell me it’s gone; tell me the alternative.
2. **Platform clarity:** Like the commuter said, the platform is the biggest stressor. If I’m at a big station like Maradana and the platform changes, I need a clear, simple notification that says, "Your train to Kandy is now at Platform 4," not just an announcement over a loudspeaker I might not understand.
3. **Station guidance:** If I have to change trains, please show me simply how to get there. Is it a long walk? Do I need to use a footbridge? 

If I’m left guessing, I’ll be lost. Please keep it simple and give me a clear path forward.

---

**moderator**

@product-owner: Both passenger personas have highlighted that "Platform Information" is their primary source of trust and anxiety reduction. However, station platforms are notoriously dynamic and often updated manually by local staff at the last second. 

How will you ensure the data feeding the app's "Platform" field is synchronized with the physical station display boards to prevent the "app vs. board" mismatch that the daily commuter warned would cause them to abandon the platform entirely?

---

**product-owner**

This is the exact operational risk that keeps me up at night. If the app says Platform 3 and the board says Platform 1, we have failed. 

To solve this, we must enforce **"Single Point of Truth" data entry**. We cannot have the app pulling from one source and the station display from another. My requirement for the Operations Web App is that the Station Master or the Platform Supervisor updates the platform number *once* in our central system. That action must trigger an API push to both the physical display boards and the mobile app simultaneously.

If the data isn't in the system, it doesn't exist. No local, manual overrides on the boards that aren't mirrored in the backend. 

**My trade-off:** I am prioritizing the **Station Terminal/Display integration** over the passenger mobile app's "live map" feature. If we can't get the data flow from the platform to the digital display perfect, the app is secondary. 

We need a "Must-Have" rule: The mobile notification and the station display must originate from the same database record. If the technology can’t guarantee that sync, we don't ship it.

---

**moderator**

MODERATOR: @daily-commuter: You mentioned that you would abandon the app if it contradicts the physical station board. If a platform change happens while you are already on the train or in a tunnel with poor connectivity, how should the app handle that "sync gap" to ensure you don't receive an outdated notification that leads you to the wrong platform?

---

**daily-commuter**

If I’m already on the train or in a tunnel, I’m not looking at my phone—I’m looking for a seat or just trying to breathe in the crush. But the moment I get a signal again, if I see an old notification about a platform that’s no longer true, I’ll lose my temper. 

The app needs to be smart enough to know when a notification is "stale." If the data is more than a few minutes old, don't show it to me. Better yet, when I open the app after a signal dead zone, it should immediately show a clear "Last updated at [Time]" timestamp. If I see it was updated ten minutes ago, I know to look at the station board instead. 

And please, stop sending me "push" notifications for old news. If a platform change happened while I was offline, just update the main screen of the app. Don't ping my phone with an alert that is already expired. That just clutters my screen and makes me think the app is broken. I want speed, not a history lesson of what went wrong while I was in the tunnel.

---

**moderator**

MODERATOR: @occasional-commuter: You mentioned needing clear instructions on alternative routes if your train is cancelled, but these alternatives often involve different operators or bus connections that may not be under SmartRail's direct control. If the app suggests an alternative route that turns out to be invalid or unavailable, how much responsibility should the app take for that guidance, and what is your expectation for "recovery" if the alternative fails?

---

**occasional-commuter**

If the app suggests an alternative, I am going to trust it because I don't know the network. If I end up stranded because the bus or connecting train wasn't actually there, my frustration will be ten times worse than if the app had just told me "no alternatives found."

If an alternative fails, the app must have a clear "Help" or "Support" button that doesn't just link to a generic website. I need to know: Is there a station guard nearby? Is there a help desk? 

For me, the "recovery" isn't about the app fixing the train—I know it can't do that. It’s about the app acknowledging the mistake and giving me a human-to-human contact point. If the app sends me on a wild goose chase, it needs to provide a way for me to get back on track, like showing me the nearest station office or a way to call someone who actually knows what’s happening. 

Don't just leave me with a "sorry, that didn't work" message. Give me a clear, simple way to find a person who can help me. That’s the only way I’ll trust it again.
