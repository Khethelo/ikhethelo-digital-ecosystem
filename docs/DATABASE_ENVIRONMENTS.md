# PostgreSQL environments

All environments use the same EF Core model, migrations and `ConnectionStrings:DefaultConnection` configuration key. They must not share data.

## Development

Run PostgreSQL locally and keep the connection string in the API project's .NET User Secrets, never in a tracked settings file. On this workstation Development uses the existing `ikhethelo_digital` database on the local PostgreSQL service at `127.0.0.1:5432`. It contains existing development data; do not reset it. The earlier separate local database `ikhethelo_digital_dev` remains in `.local-development/postgres` but is no longer the configured Development database.

If using another local instance, update `ConnectionStrings:DefaultConnection` in User Secrets without including credentials in source control. Development configuration loads User Secrets; environment variables override them. Check for `ConnectionStrings__DefaultConnection` overrides in the shell and Visual Studio before running migrations or starting the API. On this workstation, `Email:Enabled` is enabled in local User Secrets, so Development can send real email; automated tests use disabled or fake email. The `20261119231544_TenantStaffRoles` migration was applied to the local database after a backup; apply future migrations only after reviewing them and backing up any data you need.

Apply existing migrations only after verifying the configured host and database are local:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet ef database update --project IKhethelo.Digital.Api/src/IKhethelo.Digital.Infrastructure --startup-project IKhethelo.Digital.Api/src/IKhethelo.Digital.Api --context DigitalDbContext
```

Use the existing interactive `--bootstrap-admin --tenant ESHL` command to create an initial local administrator after migration. Choose a local-only password. Do not reuse production accounts or data.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project IKhethelo.Digital.Api/src/IKhethelo.Digital.Api --launch-profile IKhethelo.Digital.Api -- --bootstrap-admin --tenant ESHL
```

## Integration tests

Set `IKD_TEST_POSTGRES` to a **separate** local PostgreSQL test server with permission to create databases, using a numeric loopback address (`127.0.0.1` or `::1`). Tests fail closed for other hosts and create and delete only their own randomly named databases. Never point the test control connection at the development or production server. Tests disable email.

## Production

Render supplies Neon through the deployment secret/environment variable `ConnectionStrings__DefaultConnection`. Production does not load Development User Secrets. Keep Neon credentials and JWT keys in deployment secrets, not repository files; do not copy production data into Development or tests. Migrations and administrator bootstrap are explicit operations and do not run on startup.
