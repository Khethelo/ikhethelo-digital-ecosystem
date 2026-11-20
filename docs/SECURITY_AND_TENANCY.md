# Es'hlahleni membership workflow

Es'hlahleni is a cannabis-centred social club and hangout space: cannabis, edibles, cookies, PS5, good company and a place to relax. Preserve the existing badge, logo, colours and page layouts. No online payment, ordering, catalogue, inventory or invented commercial offerings.

Public application ? Pending ? administrator review ? Approved or Rejected. Approval creates an active member and allocates an ESC-xxxxx number before attempting a confirmation email.

TenantAdmin and TenantStaff can review applications. Only Pending applications can be approved or declined. Approve and decline use tenant-scoped row locks; repeat or competing decisions return 409. Approval creates one active member; decline creates none.

API and database statuses are Pending, Approved and Rejected. The existing database strings and constraints remain unchanged; no status data migration is needed.

Tenant ownership, authorization, consent, age, and identity uniqueness remain enforced. Review metadata and audit are committed with the decision. Membership confirmation email is attempted only after approval commits.

Confirmed contact: WhatsApp 083 726 5910 (https://wa.me/27837265910); 32 Albany Grove, Durban Central, Durban, 4001. Do not label WhatsApp as general telephone. No public email address or opening hours have been supplied. Frontend details live in src/app/content/club-content.ts.

Email uses IEmailSender and SMTP with STARTTLS. Both notifications are best effort after successful commit, with a 20-second delivery bound and safe failure logging. No automatic retry/outbox is introduced for this MVP. Failure preserves the successful HTTP/database result; repeated submission is rejected safely. Provider exceptions and credentials are not logged. HTML encodes personal values and confirmation email includes the actual generated number.

Email is disabled by default. To enable delivery configure Email:Enabled=true, Email:Host, Email:Port (587), Email:Username, Email:FromAddress and Email:FromName. Store Email:Password in .NET user secrets for the API project or Email__Password in the environment. Environment equivalents for other keys use double underscores. Never commit credentials. Enabled configuration is validated at startup. Use an SMTP account/app password supporting STARTTLS; no secret is needed for automated tests. The existing API project UserSecretsId supports local configuration.

Policy terms/privacy pages remain explicitly marked drafts requiring the owner's final text. Membership decisions follow the workflow above.

## Technical reference

The current HTTP contract is ../contracts/ikhethelo-digital-api.openapi.yaml. Persistence definitions are in DATA_MODEL.md and the baseline SQL. The implemented .NET solution contains Api, Application, Domain and Infrastructure projects; the existing Angular application is eshlahleni-frontend.

## Enforced security boundaries

TenantAdmin derives ownership from validated identity and cannot read or mutate other tenants. IKhetheloAdmin selects a tenant and requires explicit CrossTenantMembership grants; AdminUserManagement is a separate privilege. There is no unscoped all-tenant membership endpoint. Every resource access combines identity and tenant; scope failures return safe 404/403 responses without exposing another tenant's personal data.

Administrator passwords use Microsoft password hashing. JWTs use RS256, 30-minute expiry, validated issuer/audience/signature and persisted security_version. Security changes revoke earlier tokens. Tenant administrators cannot assign the internal role or move accounts between tenants. Self-deactivation and removing the last active tenant administrator are blocked. First-administrator bootstrap is an interactive trusted console operation, not a public HTTP endpoint. Authentication and administrative account responses use no-store.

Explicit DTOs exclude TenantId, hashes, ownership and audit internals. Unknown/duplicate JSON properties, invalid types, numeric enums and oversized request bodies are rejected. Body limit is 16 KiB; paging is bounded to 100. Field validation returns 422; state/uniqueness conflicts return 409. Logs avoid submitted contacts, secrets and provider exception objects. Email values are HTML-encoded, SMTP uses TLS and credentials come from protected configuration.

Age and consent remain server-enforced. Public submission is rate limited and does not provide public registration lookup or an applicant account. Configure HTTPS, allowed hosts, explicit production CORS, restricted database credentials and secret storage for deployment. Swagger UI and contract are available only in Development. Data retention, backups and final privacy/terms text remain deployment responsibilities.
