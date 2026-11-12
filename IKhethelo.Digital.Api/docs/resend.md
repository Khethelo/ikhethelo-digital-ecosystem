# Registration email

Email delivery is enabled in appsettings.json using Resend SMTP on port 587 with STARTTLS. The sender is Es'hlahleni Social Club <info@ikhethelodigital.co.za>. Verify ikhethelodigital.co.za in Resend before attempting delivery.

From the workspace root, save a newly generated Resend sending API key in local .NET User Secrets:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\IKhethelo.Digital.Api\scripts\Configure-Email.ps1
```

The script prompts with hidden input and writes Email:Password without putting the key in command history or project files. It preserves other secrets. Restart the backend after saving it. Never reuse the previously exposed key or paste a new key into chat or source files.

Successful registrations trigger the branded confirmation email after database commit. Delivery failure preserves the registration and is logged safely; previous confirmations are not automatically resent. Production deployments must supply Email__Password through protected environment configuration rather than local User Secrets.

The receipt's optional WhatsApp button contacts the club at 083 726 5910 with the saved application reference. It does not send a confirmation to the applicant and has no test-number override.
