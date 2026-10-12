# Session station-manager+customer-service-2026-10-04T10-38-57-839Z

Topic: When boards, announcements and the app disagree, who handles the passenger and who fixes the data?

**moderator**

Station-manager, when a passenger approaches your desk because the platform display shows a train that the app says has been cancelled, what is your current protocol for verifying the "source of truth," and what physical or digital tools are you lacking to resolve that discrepancy for them on the spot?

---

**station-manager**

Look, this happens far too often. My "protocol" right now is a mess of phone calls. I have to radio the control room or call the signal box to see if they’ve actually pulled the train or if it’s just a display glitch. In the meantime, I have a platform full of angry commuters for the evening peak—maybe it's 6 pm on a Friday, the station is packed, and the air is thick with frustration.

I am missing three critical things:

1. **Local Override Authority:** If the display is lying, I should be able to instantly override it with a "Service Cancelled" or "Platform Change" message from my own terminal. I shouldn't have to wait for someone in a central office to "get around to it."
2. **System Health Indicators:** My boards currently just show stale data. I need a status light or icon that tells me, "Data feed delayed—verify locally." If the network drops, the board should say so, not keep showing a ghost train.
3. **Real-time Synchronization:** The app and the board must pull from the same source. If I update a platform change, it must hit the boards *and* the app simultaneously. 

What happens at 6 pm on a Friday when the system is wrong? I lose control of the crowd, that's what.

---

**moderator**

MODERATOR: Customer-service, when the Station-Manager is forced to manually override a display or confirm a cancellation via radio, how does your team receive the "official" word that a disruption is happening, and what is your current process for reconciling the conflicting information a passenger presents to you versus what your own internal dashboard shows?

---

**customer-service**

Honestly? I usually find out about these disruptions from the passengers themselves before the "official" word ever hits my dashboard. 

When a passenger messages me or calls, frantic because they’re at Colombo Fort and the board says the Kandy express is departing but the platform is empty, I’m flying blind. My internal dashboard often shows "On Time" because it’s pulling from the outdated schedule, not the reality on the ground. 

I’m stuck in this humiliating position where I have to tell a passenger, "I see it as on-time," while they’re looking at an empty track. It makes us look incompetent. 

I need:
1. **A Live Incident Feed:** A single, authoritative stream that updates in real-time. If the Station-Manager updates a status, it should pop up on my screen immediately with the "why" so I can give a real answer, not a script.
2. **Escalation Path:** Today, I have no way to "flag" a discrepancy to Operations. I need a "Report Data Error" button that puts the live issue in front of a Controller.
3. **Approved Messaging:** Give me the exact text to send, in Sinhala, Tamil, and English, so I don't accidentally promise a refund I can't authorize.

---

**moderator**

MODERATOR: Station-manager, regarding your request for "Local Override Authority"—if you have the power to manually change a platform display during a crisis, what security controls or audit logs must be in place to ensure that this authority isn't misused or that conflicting overrides aren't triggered simultaneously by different staff members at the same station?

---

**station-manager**

Look, I’m not asking for every ticket collector or porter to have edit access! On my station floor, authority must be strictly controlled, otherwise we invite chaos.

To keep this safe and accountable, operational governance requires four things:

1. **Role and Duty-Based Access:** Only the active Shift Supervisor or Station Manager logged into that specific station—say, Colombo Fort—should hold override rights for our platforms. 
2. **Single-Editor Locking:** If I am changing a display for Platform 4, the system must lock that platform to my login. If my assistant attempts to make a change simultaneously, they must see an active lock notice showing my name.
3. **Mandatory Logging with Reasons:** Every override must log the staff member's ID, timestamp, and require a quick selection from approved operational reasons (e.g., "Track Obstruction" or "Signal Delay").
4. **Controller Alerting:** Central Operations must be notified immediately when a local override occurs, but central control shouldn't silently overwrite my local safety action without contacting me.

At 6 pm on a Friday, if two staff members post conflicting platform numbers, thousands of commuters will stampede across the footbridge. Clear local hierarchy is a safety critical requirement.

---

**moderator**

Customer-service, if a Station Manager uses local override authority to mark a train as "Cancelled" on station displays, but Central Operations hasn't officially declared that cancellation in the backend ticketing system, what specific system state or workflow must you see on your dashboard to handle passenger compensation claims without making unauthorized refund promises?

---

**customer-service**

That intermediate gap is my worst nightmare! If the Colombo Fort Station Manager marks the 5:45 PM Kandy express as "Cancelled" on the platform board, but Central Ops hasn't updated the backend, passengers instantly call us demanding immediate refunds.

On my dashboard, I must see an explicit **"Local Cancellation Pending Central Confirmation"** status. It has to clearly state *who* overrode the status (e.g., Station Manager - Colombo Fort) and at what time, so I can validate what the passenger is seeing.

My workflow must provide:
1. **Approved Trilingual Messaging:** Exact, pre-scripted text in Sinhala, Tamil, and English: *"We acknowledge the station cancellation. Your claim is logged and will be processed once Central Operations validates the incident."*
2. **A "Log Provisional Claim" Action:** A way to register their ticket and contact details without making an unauthorized payout promise.
3. **Automated Escalation:** An alert that flags this mismatch to Central Controllers if Ops hasn't reconciled the local override within 15 minutes.

The brief completely leaves out **provisional refund workflows** and compensation rules when local station actions conflict with central ticketing records!
