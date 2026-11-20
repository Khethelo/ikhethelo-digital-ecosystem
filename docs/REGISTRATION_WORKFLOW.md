# Es'hlahleni membership workflow

Es'hlahleni is a cannabis-centred social club and hangout space: cannabis, edibles, cookies, PS5, good company and a place to relax. Preserve the existing badge, logo, colours and page layouts. No online payment, ordering, catalogue, inventory or invented commercial offerings.

Public application ? Pending ? administrator review ? Approved or Rejected. Approval creates an active member and allocates an ESC-xxxxx number before attempting a confirmation email.

TenantAdmin and TenantStaff can review applications. Only Pending applications can be approved or declined. Approve and decline use tenant-scoped row locks; repeat or competing decisions return 409. Approval creates one active member; decline creates none.

API and database statuses are Pending, Approved and Rejected. The existing database strings and constraints remain unchanged; no status data migration is needed.

Tenant ownership, authorization, consent, age, and identity uniqueness remain enforced. Review metadata and audit are committed with the decision. Membership confirmation email is attempted only after approval commits.

Confirmed contact: WhatsApp 083 726 5910 (https://wa.me/27837265910); 32 Albany Grove, Durban Central, Durban, 4001. Do not label WhatsApp as general telephone. No public email address or opening hours have been supplied. Frontend details live in src/app/content/club-content.ts.

Email uses IEmailSender and SMTP with STARTTLS. Both notifications are best effort after successful commit, with a 20-second delivery bound and safe failure logging. No automatic retry/outbox is introduced for this MVP. Failure preserves the successful HTTP/database result; repeated submission is rejected safely. Provider exceptions and credentials are not logged. HTML encodes personal values and confirmation email includes the actual generated number.

Email is disabled by default. To enable delivery configure Email:Enabled=true, Email:Host, Email:Port (587), Email:Username, Email:FromAddress and Email:FromName. Store Email:Password in .NET user secrets for the API project or Email__Password in the environment. Environment equivalents for other keys use double underscores. Never commit credentials. Enabled configuration is validated at startup. Use an SMTP account/app password supporting STARTTLS; no secret is needed for automated tests. The existing API project UserSecretsId supports local configuration.

For local Resend SMTP setup, run this from the workspace root:

```powershell
.\IKhethelo.Digital.Api\scripts\ConfigureEmail.ps1
```

Enter your verified sender email address and Resend API key at the prompts. The password prompt is hidden, and settings are saved to the API's existing .NET user secrets without replacing JWT or database settings. Restart the API in Development, then submit a new registration using an inbox you control. Check that the confirmation arrives and contains the saved application reference. This command configures delivery; it does not send a test email. User secrets are for local development; use deployment secret configuration for production. Other STARTTLS SMTP providers can be configured using the script's `-HostName`, `-Port` and `-Username` parameters.

The application email includes the application reference and submission time; it does not claim membership is approved.

In Visual Studio, right-click IKhethelo.Digital.Api and choose Manage User Secrets. Set these keys there:

```json
{
  "Email:Enabled": true,
  "Email:Host": "your SMTP host",
  "Email:Port": 587,
  "Email:Username": "your SMTP account",
  "Email:FromAddress": "your verified sender address",
  "Email:FromName": "Es'hlahleni Social Club",
  "Email:Password": "your local app password"
}
```

Equivalent environment keys are Email__Enabled, Email__Host, Email__Port, Email__Username, Email__FromAddress, Email__FromName and Email__Password. Keep the actual account values outside repository JSON. Restart the API after changing configuration. Real external delivery requires these local settings and has not been attempted with a personal account during automated verification.

Policy terms/privacy pages remain explicitly marked drafts requiring the owner's final text. Membership decisions follow the workflow above.
