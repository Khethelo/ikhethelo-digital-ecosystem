# Registration workflow implementation verification

Implemented in the existing backend and Angular application on 10 November 2026 (Africa/Johannesburg).

The public registration persists an AwaitingInPersonCompletion record and audit, then attempts a registration-received email. Staff activate after the R100 step in person; activation preserves row locking, transactional member creation, sequence-based member numbers, tenant ownership, uniqueness and audit. The activation email contains the generated number and runs after commit. Repeated activation returns 409. The rejection endpoint and UI are removed. Historical database values remain compatible through EF conversion without a schema migration.

SMTP is behind IEmailSender with validated enabled configuration, TLS, bounded delivery and safe failure logging. Personal credentials were neither requested nor configured. Real external delivery is disabled until local settings in REGISTRATION_WORKFLOW.md are supplied. Automated email tests use fakes and verify committed state from an independent database context, successful-state preservation during delivery failures, recipient, encoding, venue details and actual member number.

The existing logo, SCSS direction and layouts remain. Public copy now describes the cannabis-centred hangout experience including PS5, edibles and cookies. Shared confirmed venue/WhatsApp content appears on contact, footer and registration success. Opening hours and invented public email were removed. Policy terms/privacy remain labelled drafts requiring owner content.

Executed verification:

- Backend Debug build passed; final Release full rebuild passed with zero warnings/errors.
- Final Angular production build passed.
- Angular unit tests: 12 passed across 3 files.
- OpenAPI/frontend contract check passed. The database-backed contract test also passed route/schema matching and PostgreSQL baseline/model equivalence.
- Live browser acceptance against a disposable migrated PostgreSQL database: all 3 tests passed. Covered mobile layout/navigation, registration, status/contact/R100 instructions, authenticated activation, actual member number, repeated-activation conflict, edits/status updates, pagination, error handling and session expiry. Homepage/mobile and admin screenshots were inspected.
- Backend database-backed suite: 57 of 58 passed, including both post-commit email integration cases and tenant/concurrency tests. The remaining test's assertion incorrectly expected no registration audit after failed activation. It was corrected to preserve RegistrationCreated while requiring no activation audit/member. Final reruns were blocked before discovery by Windows Application Control (0x800711C7), including after a full rebuild outside the sandbox. A completely green final backend suite is therefore not claimed.

Tests used a disposable local PostgreSQL server at port 55439 and temporary databases. No club database or personal SMTP account was used. Initial connection failures and Angular sandbox directory failures were environment issues; Angular checks succeeded outside the sandbox. Final backend test results record the assembly-load restriction in IKhethelo.Digital.Api/TestResults/registration-workflow.trx.

Developer follow-up: configure SMTP locally, finalise policy copy, and rerun the backend suite on a host where Windows Application Control permits loading the test assembly. No security policy was disabled or weakened during verification.
