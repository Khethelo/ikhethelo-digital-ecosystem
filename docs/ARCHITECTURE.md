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

## Application boundaries

Api owns HTTP, strict JSON input, authentication/authorization, tenant resolution, rate limits and safe Problem Details. Application owns DTOs and use-case interfaces, including IEmailSender. Domain contains entities and status enums without EF or HTTP dependencies. Infrastructure implements use cases, EF persistence, audit, identity and SMTP. Program.cs is the composition root. Thin controllers call focused use cases; no generic repository or distributed messaging layer is introduced.

Tenancy resolves public alias eshlahleni to ESHL; clients never submit an internal TenantId. TenantData scopes application/member queries and inserts use the authorized tenant. Internal administration requires explicit grants and audited selected-tenant access. Practona remains a separate application/API/database; this shared backend hosts reusable platform capabilities.

PostgreSQL and EF migrations own storage evolution. Baseline SQL is a reference and existing-database adoption verifies it before adding migration history. Do not execute baseline CREATE TABLE statements as an upgrade. Activation locks the selected tenant's registration FOR UPDATE, checks active identity conflicts, allocates member_number_seq, inserts the member, updates registration metadata and writes audit logs in one transaction. Sequence gaps on rollback are acceptable. Database uniqueness provides the final concurrency guard.

Submission saves registration and RegistrationCreated audit in the same SaveChanges transaction. Notifications run only after that transaction or the explicit approval transaction commits. Notification delivery does not use the request-aborted token after commit; a separate timeout limits delivery. IEmailSender can be replaced without changing membership persistence. There is no outbox, background delivery retry or email-delivery status endpoint in this MVP.

Angular uses standalone lazy components, Router, Reactive Forms, HttpClient and typed models. Public pages share the existing SCSS and badge assets; admin authentication remains in memory. Contact information is a typed shared content object. Production must serve the Angular build with SPA fallback and proxy /api to the API over HTTPS.
