# Es'hlahleni membership workflows

Online: a visitor submits the public membership application; it remains Pending until an authorized administrator accepts or declines it. Acceptance creates an active member.

Walk-in: an organization administrator or club admin selects **Register Member** on the dashboard or Members page, captures the visitor's date of birth and contact information, and confirms both consents. The existing service creates an Approved internal application and an Active member, generates an `ESC-` number from the shared sequence, and commits both with audit entries. It then attempts the same confirmation email used for online approvals. If delivery is disabled or fails, the membership remains active and the screen reports the email result.

Pending applications and active members with matching email or phone prevent direct registration. Review an existing pending application instead of registering it again. Tenant access rules still apply; staff cannot manage administrators.

The protected ESHL registration response includes the matching pending application's ID so the administrator can open it directly. Anonymous duplicate submissions never receive that identifier. A concurrent database conflict can instead return a generic conflict.

Development uses local PostgreSQL and email is disabled unless explicitly configured. Integration and browser tests use separate disposable databases on a numeric loopback PostgreSQL host and disable or fake email. Production uses deployment secrets; no migrations or bootstrap are performed automatically.

Manual error checks: try an incorrect password and an unknown account, then access User Management as staff. Stop the local API to check the unavailable-service message. Try pending-application matches by email and phone, an active-member duplicate, invalid contact details and an under-18 date of birth. Check that the pending conflict links to its application and preserves the form, that missing records report 404, and that an invalidated session returns to sign-in. With a simulated email failure, confirm the member remains active and the screen reports the delivery failure separately.
