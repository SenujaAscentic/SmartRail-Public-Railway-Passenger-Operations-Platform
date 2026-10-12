# Session qa-engineer+product-owner-2026-10-04T10-51-20-034Z

Topic: Which five end-to-end flows matter most, and what are the acceptance criteria?

**moderator**

@qa-engineer, regarding the "Passenger Ticket Purchase" flow, what specific edge cases or failure scenarios—such as payment gateway timeouts or seat inventory conflicts—must be handled to consider this flow successfully validated?

---

**qa-engineer**

Look, "payment success" is the easy path. That doesn't tell me if the system is robust. I need to know exactly what happens when the network drops while the passenger is hitting "Pay" at a crowded Colombo Fort platform.

Here is what I need to see handled:

1. **The "Double-Tap" & Race Conditions:** What happens if the user clicks "Buy" twice due to lag? Do we charge them twice? I need a unique idempotency key for every transaction.
2. **Gateway Timeouts:** If the payment gateway (like a bank API) hangs, does our app hang, or does it return a graceful "Processing" state? What is the timeout threshold—10 seconds? 30?
3. **Inventory Contention:** If the last 1st-class seat is grabbed by two people simultaneously, who gets it? Does the loser get an immediate, clear refund or a credit?
4. **Offline/Partial Sync:** If the app loses signal *after* the payment is processed but *before* the ticket is rendered, how does the user recover their ticket?
5. **Session Expiry:** Does a ticket in the cart expire? If it does, what is the exact UX?

Give me numbers for these timeouts. Without them, I can’t write a single test.

---

**moderator**

@product-owner, regarding the QA-Engineer's requirements for handling "Inventory Contention" and "Session Expiry," what is the business priority: is it better to hold a seat in a "pending" state for a user for a fixed duration, or should we prioritize immediate availability to ensure maximum ticket throughput, even if it risks frustrating users who get interrupted during checkout?

---

**product-owner**

Listen, I appreciate the technical rigor, but let’s ground this in reality. We are building for commuters at Colombo Fort, not a high-frequency stock exchange.

**My decision:** We prioritize **user trust and clarity** over raw throughput for the MVP. 

If a passenger has reached the checkout screen, they’ve already invested time. Frustrating them with a "seat gone" message because of a 30-second delay is a failure of the passenger experience.

1. **Holding the Seat:** We will implement a **5-minute "soft hold"** on a seat once the checkout process initiates. It’s a small enough window to prevent inventory from being locked indefinitely, but long enough for a passenger to complete the payment despite poor mobile data.
2. **Inventory Contention:** If the hold expires, the seat is released. If two people hit the gate at the exact same millisecond, the first request to reach our backend wins; the second receives a "Sorry, this seat is no longer available" message *before* any payment is processed.
3. **Payments:** I will not accept "pending" states that leave a passenger’s money in limbo. If the gateway times out, the transaction is void. No money, no ticket.

**Important:** We are **deferring** complex multi-seat booking and seat selection for the MVP. We start with "Best Available" to reduce inventory complexity. 

@qa-engineer, does this 5-minute window work for your test cases?

---

**moderator**

MODERATOR: @qa-engineer, given the Product Owner's decision to move to a "Best Available" seat allocation model to simplify inventory, how do we validate the "Partial Sync" failure scenario—where a payment is successfully processed but the app loses connectivity before the "Best Available" seat assignment is confirmed and rendered to the user?

---

**qa-engineer**

A 5-minute soft hold is a start, but it doesn't solve the "partial sync" nightmare. If the user pays, the bank takes the money, but the app doesn't get the "Ticket Confirmed" packet, the passenger is left standing on the platform with a debit alert and no QR code. 

That isn't just a "failure"—that's a support desk disaster.

Here is how I’m testing this:

1. **The "Truth" Source:** The app *must* poll the server for the transaction status using the unique transaction ID once connectivity returns. If the backend says "Paid," the app must render the ticket immediately, regardless of what happened during the initial handshake.
2. **Atomic Confirmation:** Does the backend treat the payment and seat assignment as a single atomic transaction? If the payment succeeds but the inventory update fails, does it trigger an automatic reversal? I need a "Cleanup" script to verify no "ghost" payments exist.
3. **The "Offline" UX:** If the app is offline, it must clearly state: "Payment processing—please do not close the app." If they close it, what is the recovery path? 

I need to see the API specs for these sync states. What is the status code for "Payment received, seat pending"? 202? 200? I need the exact contract.

---

**moderator**

MODERATOR: @product-owner, in the event of the "partial sync" scenario where the payment is captured but the seat assignment fails due to a backend error, do we have a defined business policy for automatic refunds, or does this require manual intervention by the SmartRail support team?

---

**product-owner**

This is exactly why I pushed for a "Best Available" seat model—to minimize the logic gaps where things can go wrong.

**My decision:** We will **not** rely on manual intervention for these cases. We do not have the staff to handle thousands of manual refund requests, and it would destroy our reputation on day one.

1. **Automatic Reconciliation:** We must have an automated "Reconciliation Service." If a payment is captured but the seat assignment fails (or times out), the system must trigger an **instant, automated reversal** to the payment provider. The passenger gets a push notification: "Transaction failed, your money is being returned."
2. **Atomic Operations:** The backend must treat the ticket issuance as an atomic transaction. If the inventory service doesn't return a "Seat Locked" status, the payment gateway must be instructed to void or refund immediately. No "pending" status should exist for more than 60 seconds.
3. **UX Requirement:** The app must show a clear "Retry" button that polls for the *actual* status of the transaction ID, not the local app state.

@qa-engineer, focus your tests on the **Reconciliation Service**. If it fails to refund within 5 minutes, that’s a critical bug. We aren't building a "support desk" product; we are building a reliable transport system. Keep it simple.
