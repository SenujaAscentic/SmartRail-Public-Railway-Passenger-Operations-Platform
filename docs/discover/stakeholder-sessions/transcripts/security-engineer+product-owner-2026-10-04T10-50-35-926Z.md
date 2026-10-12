# Session security-engineer+product-owner-2026-10-04T10-50-35-926Z

Topic: What passenger data do we collect, how long do we keep it, and what must not be logged?

**moderator**

Welcome. As our security engineer, what specific types of passenger data—such as payment credentials, PII, or real-time location tracking—must absolutely be excluded from all SmartRail application and system logs to meet our security and compliance mandates?

---

**security-engineer**

Who has access to our log aggregation tools? If operations or third-party vendors can read them, then raw payment credentials, National Identity Card numbers, phone numbers, and live GPS coordinates of passengers must never touch the logs. 

If we log real-time passenger tracking or full credit card numbers, one compromised log store exposes our entire user base. We log anonymised event IDs and tokenised transaction references, nothing more.

---

**moderator**

PRODUCT-OWNER: If security restricts live location and contact details from our operational records, how will customer support verify a passenger's journey during dispute resolution, and what is your required retention period for this travel history before it must be permanently purged?

---

**product-owner**

That is a very fair question from customer support, but we need to draw a sharp line here. Security is right that we must protect our passengers' live tracking data and raw payment credentials—we cannot have those sitting unprotected in log files. 

However, customer support dispute resolution does not require spying on a passenger's live GPS coordinates. What support actually needs is transactional and journey history: which ticket was bought, what service they booked, which class (1st, 2nd, or 3rd), and whether validation scans occurred at the station gates like Colombo Fort or Maradana. 

Here is my trade-off decision:
- **Must Have:** Securely store tokenised ticket and journey histories in our database—not raw log files—so support can look up past trips when a commuter complains about a delayed intercity train or a ticketing error. 
- **Should Have:** A clear data retention policy. Let's keep passenger travel history for a maximum of 90 days for dispute resolution, after which it must be permanently purged or fully anonymised to respect user privacy.
- **Deferred / Out of Scope:** Real-time passenger location tracking in support logs. We track the *train's* position for operations, not individual passengers.

Let's rank our needs and keep the scope tight.

---

**moderator**

SECURITY-ENGINEER: Given that 90 days of station gate scans effectively reconstruct a passenger's physical travel patterns, what specific access and de-tokenization controls must we enforce so support agents can look up a dispute without exposing that movement history to unauthorized staff?

---

**security-engineer**

Who can call the lookup tool, and who audits the auditors? 

If a support agent pulls up 90 days of gate scans for someone commuting daily from Gampaha to Colombo Fort, that is raw surveillance data. 

Here is what must be protected:
1. **Least Privilege & Role Separation:** Customer support staff must never see raw passenger identities linked directly to historical movement. We need role-based access control where support agents see only tokenised IDs. De-tokenisation—revealing a passenger's name or phone number—must require a dual-authorization sign-off or a senior operations manager's override.
2. **Mandatory Audit Trails:** Every single lookup of travel history must write an immutable, append-only audit log: who accessed it, which ticket ID was queried, and what dispute reference ticket was attached. If an agent looks up a profile without an active customer complaint ticket, an automated alert must fire to security operations.
3. **Encryption at Rest:** Historical journey data sitting in our database for those 90 days must be strongly encrypted with keys rotated regularly, separate from application operational data. 

If we don't enforce strict access controls on this data, our own support portal becomes a stalking tool.

---

**moderator**

PRODUCT-OWNER: Since the system will permanently purge or anonymize travel history after 90 days, how will this policy handle active legal disputes, fraud investigations, or government subpoenas that require retaining a specific passenger's journey logs beyond that window?

---

**product-owner**

That is a serious compliance and legal reality we cannot ignore. While our standard default for everyday commuter disputes is a 90-day purge to protect privacy, we must have a lawful hold mechanism. 

Here is my product decision for handling this exception:

- **Must Have:** A legal/fraud "Hold Flag" in our database admin console. When an official subpoena, active fraud investigation, or legal dispute is logged by our legal department against a specific tokenised account or ticket reference, authorized compliance officers can place a retention lock on that specific record. This freezes the 90-day auto-purge countdown for that exact dataset only.
- **Should Have:** Clear logging on who applied the legal hold and why, tied to an official ticket reference number from our legal team.
- **Deferred / Out of Scope:** Keeping all passenger records indefinitely "just in case." Blanket long-term retention of everyone's daily commute history across the Main Line or Coastal Line is a massive liability. We purge by default; we retain only under strict, auditable legal holds.

Let's make sure our database architecture supports record-level retention exceptions without breaking our automated privacy cleanup jobs.
