# NTM local development setup — 13 November 2026

The operator confirmed that `localhost:5432/ikhethelo_digital` is a local development database. No production database or Windows security policy was changed.

## Completed local setup

- Development JWT issuer, audience and a generated 2048-bit RS256 private key are stored in .NET User Secrets (`ikhethelo-digital-api-development`). No private key is committed. The generator rotated the development key and invalidated previous development tokens.
- Local User Secrets enable `Tenancy:NtmTenantCodes:0=NTM` and disable outbound email.
- IIS Express successfully hosts the API at `https://localhost:44385`. Angular can target it with `NTM_API_TARGET=https://localhost:44385`.
- Before database reconciliation, a custom-format PostgreSQL backup was created outside the repository: `%LOCALAPPDATA%\Temp\ikhethelo-local-backups\ikhethelo_digital-20261113-153030.dump`. It contains application data; protect it accordingly.
- Read-only inspection found empty NTM tables based on the supplied SQL, with only the first two EF migrations recorded. Strict adoption initially refused the schema differences.
- The empty NTM tables were aligned in place with the EF model: source fields and generated registration columns were added, constraints/indexes/defaults aligned, and NTM timestamp triggers removed to follow application-managed timestamps. No existing domain rows or tables were deleted. Existing Es'hlahleni tables were not altered.
- The existing strict schema-adoption verifier subsequently confirmed an exact workbook-aligned schema match and recorded all four migration IDs. History was not stamped without verification.
- The missing NTM tenant was provisioned using the migration's code-based insert, generated UUID and `ON CONFLICT DO NOTHING` behavior. NTM is active and has zero administrators.
- Interactive bootstrap was verified to reach `Initial TenantAdmin for NTM. Email:` and then ended with empty-input validation, without supplying credentials or creating an account.

## Create the first administrator

Run in your own interactive PowerShell terminal so the password stays private:

```powershell
Set-Location 'C:\Users\thand\Documents\iKhethelo Digital Projects\ikhethelo-digital\IKhethelo.Digital.Api\src\IKhethelo.Digital.Api'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet .\bin\Debug\net10.0\IKhethelo.Digital.Api.dll --bootstrap-admin --tenant NTM
```

Enter an email, a unique 12–128 character password and confirmation. Password input is hidden. Bootstrap hashes the password and writes the required audit record atomically; it refuses repeat setup. This command creates no web listener and performs no migrations. After success, sign in at `http://localhost:4300` while IIS Express is running.

No administrator has been created by the automated setup. Authenticated NTM frontend/database access remains unverified until this interactive step succeeds.
