# Es'hlahleni membership and administrator workflow

Public submissions create Pending applications, not members. TenantAdmin and TenantStaff can review applications for their own tenant. Accepting a Pending application locks it, creates one Active member with an ESC-prefixed sequence number, records the reviewer and audit, and commits before attempting the confirmation email. The approval response contains the member and `confirmationEmailSent`; false means the membership remains active but email was disabled or could not be delivered. Declining records Rejected, reviewer and audit without creating a member or allocating a number. Repeated or competing decisions return 409.

TenantAdmin is labelled Organisation Administrator and can manage tenant administrator accounts. TenantStaff is labelled Admin and can perform membership operations but cannot call administrator-management endpoints. Both can change their own password. Administrators can reset another tenant user's password; the final active TenantAdmin cannot be demoted or deactivated. Password changes, resets, role changes, deactivation and login-email changes invalidate existing tokens through the shared security version. All authorization and tenant ownership checks are enforced by the API.

The authenticated `GET /api/v1/auth/password-policy` endpoint returns the guidance used by the shared administrator password validator. Es'hlahleni displays that response and sends passwords without trimming; the API validates and hashes them.

Email uses the existing SMTP `IEmailSender`; Resend can be configured through its SMTP service. Email is disabled by default. Configure `Email__Enabled`, `Email__Host`, `Email__Port`, `Email__Username`, `Email__Password`, `Email__FromAddress`, and `Email__FromName` outside source control. Delivery has not been verified against a live provider by the automated tests. No automatic retry is provided.

The existing persisted status strings already match Pending, Approved and Rejected, so no database migration or data rewrite is required for these status names. The legacy `/activate` endpoint remains for compatibility; new clients use `/approve` or `/decline`.

Es'hlahleni receives Pending, Approved and Rejected. Other tenants retain their previous response names and status filters for compatibility.
