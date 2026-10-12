# Session catering-provider+product-owner-2026-10-04T10-47-44-554Z

Topic: Who owns menus, payment, refunds and missed collections?

**moderator**

Welcome, catering-provider. To help us map out the financial flow, do you expect to process passenger payments and refunds directly through your own merchant gateway, or are you expecting SmartRail's system to collect the funds and settle with you later?

---

**catering-provider**

Ayubowan. Let me be very clear on this. We are a separate commercial entity, not a department of the railway. Our margins are tight, especially with current food costs in Colombo. 

We strongly prefer passengers to pay directly through our own merchant gateway at the time of ordering, or pay cash on delivery (LKR) at the platform. If SmartRail collects the funds, we get tied into your bureaucratic settlement cycles. We cannot wait weeks for our money.

Furthermore, direct payment keeps the liability clean. If a train like the *Podi Menike* is delayed by three hours at Rambukkana and the hot food we prepared spoils, that is an operational failure on your end. Under our contract, once the cut-off time passes and food is prepared, we must be paid in full. If SmartRail collects the money and decides to refund a passenger for a delayed train, that refund must come out of the railway's pocket, not ours. 

So, we want direct payment integration. How will your platform handle passing the order reference to our gateway securely without leaking passenger profiles?

---

**moderator**

Product Owner, the catering provider is insisting on passengers paying directly into their own merchant gateway to avoid our settlement cycles. Does our product vision support a split-checkout experience where passengers pay different vendors separately, or are we requiring a single, unified checkout where SmartRail collects all funds and handles the downstream reconciliation?

---

**product-owner**

Let’s be completely realistic here. Our budget is tight, and our team is small. Building a unified checkout with downstream reconciliation is a massive scope creep. It introduces huge risks around payment ownership and financial audits that we simply cannot handle for the MVP. 

My decision is clear: SmartRail will *not* collect funds for catering in this release. Unified payment processing is deferred. 

The smallest useful version of this integration is a simple hand-off. The passenger selects their food in the SmartRail app, and we redirect them to your gateway or external portal, passing only the necessary journey details—like the train ID and the collection station, say, Gampaha or Colombo Fort. Otherwise, we stick to Cash on Delivery (LKR) on the platform.

Catering partner, let’s make the trade-off explicit. Is a direct redirect to your portal a *Must* for your launch, or can we start with COD to get this off the ground? We need to deliver value to the passengers quickly without delaying the entire platform.

---

**moderator**

Catering-provider, if we start with Cash on Delivery (COD) on the platform to speed up the launch, what is your process for handling "no-show" passengers who order food but aren't there to collect and pay when the train arrives? Who absorbs the cost of that wasted food?

---

**catering-provider**

Aiyo, COD is a massive risk for us. If we prepare a hot chicken lamprais for a passenger to collect at Gampaha, and they don't show up because they missed their train at Maradana, that food is wasted. We cannot resell it.

If the railway wants to launch with COD to save on your tech integration, then SmartRail must underwrite the risk. We will invoice the railway monthly for every confirmed COD order that is a no-show. Our staff on the platform will log the missed collection, and the railway must pay us the full menu price in LKR. 

If you cannot agree to absorb this cost, then COD is a non-starter. We must have the online payment redirect from day one. When a passenger pays upfront, the risk is gone. If they miss the train, we still get paid, and they can sort out refunds with you.

Also, we need a strict two-hour cut-off time before the train arrives at the collection station. No last-minute cancellations. Our margins in Colombo are too thin to play games with wasted stock.

---

**moderator**

Product-owner, the catering provider is demanding that SmartRail underwrite 100% of the financial risk for unpaid Cash on Delivery (COD) "no-shows," or alternatively force a strict online payment redirect from day one with a two-hour lock-in window. Are you willing to have SmartRail absorb these no-show liability costs, or does this push us to mandate their online payment redirect for the MVP despite your previous scoping concerns?

---

**product-owner**

Let me make this crystal clear: SmartRail will *not* underwrite no-show meals or pay for spoiled food out of railway funds. Our executive leadership will never approve absorbing private vendor losses. 

So, Cash on Delivery is officially off the table. 

If catering is in the MVP, your external payment redirect is now a **Must**, owned 100% by you. SmartRail will hand off the passenger to your platform along with the core journey details—train service ID, collection station like Gampaha or Colombo Fort, and scheduled ETA. Your gateway collects the LKR, your terms enforce your two-hour cut-off, and your platform manages refunds and payment disputes. 

SmartRail’s responsibility ends at passing train delay updates so your platform staff know when a service is running late.

Here is the hard trade-off: if you cannot deliver that external redirect on your side by our MVP launch date, the catering feature is completely **Deferred** to Release 2. Our priority is train tracking, schedule accuracy, and disruption alerts. The core platform will not be delayed for food orders. 

Can your technical team support this redirect, or do we shelve catering for phase one?
