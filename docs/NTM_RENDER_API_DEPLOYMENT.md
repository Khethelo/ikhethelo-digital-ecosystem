# NTM API Render preparation

The Dockerfile uses the repository root as its build context because the API references the shared OpenAPI contract. No migrations or administrator bootstrap run at startup. `appsettings.json` files are excluded from the image; provide production configuration through Render environment variables. The container defaults to Production and binds HTTP on `0.0.0.0:$PORT` (default 10000).

## Render settings

- Web Service, Docker runtime, Free instance, `main` branch.
- Root Directory: blank (repository root); Docker build context: `.`; Dockerfile Path: `./IKhethelo.Digital.Api/Dockerfile`.
- Docker command: blank. The staging option below is for limited verification only, not real client data.
- No build-time secrets, automatic migration, or automatic bootstrap.
- Alternatively, `render.yaml` defines the same Free Docker service as a Render Blueprint. Render prompts for the unsynchronized environment values when the Blueprint is created. Its edge-TLS setting is for the limited staging configuration described below; it is not approval for production client data.

## Environment variables

Set these through Render's environment settings, not source control:

- `ConnectionStrings__DefaultConnection`: intended Neon connection with verified TLS.
- `Jwt__Issuer`, `Jwt__Audience`, `Jwt__PrivateKeyPem`: stable production values, including an RSA private PEM of at least 2048 bits. Changing the signing key invalidates existing tokens.
- `Tenancy__NtmTenantCodes__0`: the approved tenant code `NTM`.
- `AllowedHosts`: explicitly permit the assigned Render hostname and any other API hostnames required by the actual proxy topology.
- `ReverseProxy__KnownProxies__0` and/or `ReverseProxy__KnownNetworks__0`: verified immediate proxy addresses or CIDR ranges, if Render provides them; add indexed entries for additional proxies/networks. Do not trust all addresses as a shortcut.
- `ReverseProxy__EdgeTerminatesTls`: set `true` for limited Render staging only when the public edge enforces HTTPS and private-network access to the service is controlled. This disables application-level HTTPS redirection; it does not trust forwarded headers or alter rate limiting. Render redirects public HTTP to HTTPS. Private-network traffic can reach the app over HTTP and must not carry secrets without an appropriate protected path. Leave unset to retain the normal application redirect.
- `Email__Enabled`: explicitly choose `false` for NTM-only operation or configure `Email__Host`, `Email__Port`, `Email__Username`, `Email__Password`, `Email__FromAddress` and `Email__FromName` when email is required. Disabling email affects existing membership workflows.
- `Cors__AllowedOrigins__0`: only if direct cross-origin browser access is intended. A same-origin Netlify `/api` proxy does not need browser CORS. Configure `Tenancy__Aliases__eshlahleni` and `Membership__Tenants__ESHL__RequireAdult` / `Membership__Tenants__ESHL__MemberNumberPrefix` if the existing ESHL workflow is deployed; these settings were in local, git-ignored settings files and are not in the image.
- `PORT` is provided by Render, normally 10000. `ASPNETCORE_ENVIRONMENT` defaults to `Production` in the image.

## Proxy and release blockers

Render terminates HTTPS upstream. ASP.NET Core accepts forwarded headers only from known proxies or networks, and handles one forwarding hop by default. Confirm the immediate peer addresses and X-Forwarded-For chain with Render for this service and verify spoofing resistance before relying on the client IP for login rate limiting. Netlify's additional proxy may alter the chain. Until verified, forwarded headers may be ignored, HTTPS redirect/HSTS behavior may be incorrect, and rate limiting may group clients behind a proxy. Do not clear trusted proxy lists globally. Render private-network access also needs consideration when deciding what peers to trust.

For initial staging without verified proxy addresses, do not configure any additional proxy entries; set `ReverseProxy__EdgeTerminatesTls=true`. Public HTTPS is handled by Render. The per-IP login limiter remains active but may group users by proxy address and is not a reliable per-client control; in-memory limits reset on restart and are not shared across instances. ASP.NET Core cannot determine the original HTTPS scheme from untrusted headers, so its HSTS middleware may not emit a header. Verify edge behavior separately. Do not use this configuration for real client data until proxy trust, login protections and HSTS are verified. Use an isolated staging database and staging administrator rather than testing mutations against production.

The repository may contain uncommitted API and NTM changes. Render builds the selected Git branch, not this working tree: review and commit the intended complete changes before a later approved push/deploy. Ensure exposed database credentials are rotated before real data is used. Verify the Docker image, TLS database connectivity on Linux, proxy behavior, login/authorization and audit recording before production use. Development Swagger is intentionally unavailable in Production.

After those checks and a separate approval to deploy, create the service with the settings above, enter environment values, deploy the reviewed commit, and inspect startup logs without displaying secrets. Test the service through HTTPS before connecting the frontend. Do not use the production database for destructive integration tests.
