# Session security-engineer+customer-service-2026-10-04T10-49-28-368Z

Topic: What passenger and operational data may customer service see, and how is it audited?

**moderator**

@security-engineer, considering the sensitivity of passenger PII, what specific technical controls and logging mechanisms will be in place to ensure that customer service representatives can only access data strictly necessary for their current support ticket, and how will those access logs be protected from tampering?

---

**security-engineer**

I’m glad you asked, but let’s stop treating this as a post-it note on the wall. 

"Customer service" is a massive attack vector. If a rep can search the entire database, they can scrape it. We need **attribute-based access control (ABAC)**. A rep shouldn't just "have access"; they should only see data for a passenger if there is an *active, open ticket* assigned to them. 

Who can call the API to fetch user details? It must be restricted to the support platform, not a public endpoint. 

As for the logs, they are useless if the attacker can delete them. We need **write-only, immutable audit logs** shipped off-host in real-time to a secure, centralized logging server. If a rep accesses a record, it’s logged. If they try to modify a ticket to gain unauthorized access, that’s logged. 

My concern: The brief doesn't mention **data masking**. Even if they have a ticket, they don't need to see the full passenger profile. Mask the PII. If they don't need the phone number to solve the issue, why are we showing it to them? 

What happens when a rep’s account is compromised? That’s our biggest risk.

---

**moderator**

@customer-service, how do your teams currently handle passengers who request support on behalf of someone else (e.g., a parent booking for a child), and how would the proposed ABAC system distinguish between the "ticket owner" and the "passenger" to ensure you still have the necessary data to resolve the issue without violating privacy constraints?

---

**customer-service**

That is a massive pain point for us. Right now, I’m often dealing with a father in Gampaha calling because his daughter’s train from Kandy is delayed, or a tourist who bought tickets for their whole group. 

If I can’t see the booking details because the account belongs to the father, I’m stuck. I end up asking them to read out long booking references or PNRs, which is slow and frustrating when the signal is dropping.

The current brief completely ignores this "proxy" relationship. If your ABAC system is too rigid—only letting me see the "ticket owner"—I won't be able to help. We need a way to link a "support requester" to a "passenger" or "booking reference" temporarily. 

And please, don't just mask everything. If I’m dealing with a refund for a 1st-class ticket on the Main Line, I need to see the class and the fare paid to verify the claim. If you mask the amount, I can't process the refund. I need "need-to-know" access, not "zero access." How do we handle these authorized overrides without opening a security hole?

---

**moderator**

@security-engineer, how do we technically implement an "authorized override" or "break-glass" protocol that allows a customer service representative to temporarily access linked passenger data for proxy support requests, without creating a permanent, exploitable loophole in the ABAC policy?

---

**security-engineer**

"Authorized override" is just a polite term for "privilege escalation." If you build a back door, it will get kicked in. 

If a rep needs to access data for a third party, that isn't an "override"—it’s a **delegated authorization workflow**. The system should require the ticket owner to grant temporary, time-bound consent via their own app or a secure link sent to their registered contact. We verify the *consent*, not the rep's claim of a "proxy relationship."

Regarding your "break-glass" requirement: it must be a **dual-control** event. If a rep needs to bypass standard ABAC for an emergency, they shouldn't be able to do it unilaterally. It should trigger an immediate alert to a supervisor, and require a secondary authorization code. Every second of that access must be logged as a high-severity event.

My concern remains: the brief is silent on **identity proofing**. How do you know the person calling from Gampaha is who they say they are? If you don't have strong authentication for the *passenger*, the rep is just a glorified data-exfiltration tool for whoever happens to know a PNR. You’re asking for access; I’m asking who is actually on the other end of the line.

---

**moderator**

MODERATOR: @customer-service, following up on the security-engineer's point about identity proofing, what is your current verification process when a caller claims to be the passenger or an authorized proxy, and how would you handle a scenario where the system denies access due to a failed identity check—are there manual verification steps you expect to perform, or would you expect the system to provide a "challenge" mechanism for you to present to the caller?

---

**customer-service**

A secure link sent to the app? That sounds nice in a Colombo office, but on a crowded 3rd-class commuter train from Veyangoda, mobile signal drops constantly. Half our older passengers don't even use the app—they buy physical tickets at the station counter! If you force us to rely on a "consent link," we will turn away desperate people.

Right now, we verify identity by asking for their National Identity Card (NIC) number, the booking reference, or journey details. 

If a check fails because a passenger has no signal, a hard lockout is a disaster. I’m the one who gets yelled at. We need the system to provide a "challenge-response" mechanism. Give me safe, non-PII questions to ask them—like confirming the destination station, or the approximate fare paid in LKR. 

The brief completely ignores offline validation. If the system denies access because a passenger can't get an SMS in the Rambukkana tunnels, what is my escalation path? Do I just tell them to get off the train? We must have manual bypass procedures that are logged, but still let us help.
