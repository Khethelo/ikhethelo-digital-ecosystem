# NTM GreenPulse extension (workbook verified, 12 November 2026)

The NTM request authorizes two additional tenant-owned entities, `NtmVeteran` and `NtmBusiness`, using this same context/database and audit/administrator tables. See [NTM architecture and schema](NTM_MODULE.md) and [verified spreadsheet mapping](NTM_SPREADSHEET_FIELD_MAPPING.md). NtmWorkbookAlignment adds source-preserving fields and aligns SQL precision/indexes after workbook verification; ambiguous source records still require reconciliation before any import. Deployment remains an explicit operator action. Existing Es'hlahleni definitions and historical migrations below remain intact.

# Current registration workflow (9 November 2026)

Es'hlahleni is a cannabis-centred social club and hangout space: cannabis, edibles, cookies, PS5, good company and a place to relax. Preserve the existing badge, logo, colours and page layouts. No online payment, ordering, catalogue, inventory or invented commercial offerings.

Online registration → registration-received email → Awaiting In-Person Completion → visit the club and complete the R100 membership step → staff Activate Membership → Active member and ESC-xxxxx member number → membership-active email.

Registration is not a discretionary approval process. There is no rejection action. Staff confirm in-person completion before activation. Public submission never creates a member or allocates a number. POST /api/v1/admin/tenants/{tenantCode}/membership-applications/{applicationId}/activate returns the Active member; repeated or racing activation returns 409 without duplicate members or emails.

Public and admin DTOs use AwaitingInPersonCompletion and Completed. LegacyClosed represents read-only historical closed records. Database strings Pending, Approved and Rejected, table names, sequence, constraints, indexes and review metadata columns remain unchanged through explicit EF conversion. No migration or data deletion is required. Existing SQL and migrations describe storage, not current business labels.

Tenant ownership, authorization, age 18 in Africa/Johannesburg, consent, normalization, pending-email/phone uniqueness and active-member uniqueness remain enforced. Activation uses row locking, transaction, sequence allocation, member insertion and audit commit. RegistrationCreated and MembershipActivated audits distinguish the steps. Existing member status audits remain supported. Existing direct staff registration is an in-person completion path and also sends activation email.

Confirmed contact: WhatsApp 083 726 5910 (https://wa.me/27837265910); 32 Albany Grove, Durban Central, Durban, 4001. Do not label WhatsApp as general telephone. No public email address or opening hours have been supplied. Frontend details live in src/app/content/club-content.ts.

Email uses IEmailSender and SMTP with STARTTLS. Both notifications are best effort after successful commit, with a 20-second delivery bound and safe failure logging. No automatic retry/outbox is introduced for this MVP. Failure preserves the successful HTTP/database result; repeated submission is rejected safely. Provider exceptions and credentials are not logged. HTML encodes personal values and activation includes the actual generated number.

Email is disabled by default. To enable delivery configure Email:Enabled=true, Email:Host, Email:Port (587), Email:Username, Email:FromAddress and Email:FromName. Store Email:Password in .NET user secrets for the API project or Email__Password in the environment. Environment equivalents for other keys use double underscores. Never commit credentials. Enabled configuration is validated at startup. Use an SMTP account/app password supporting STARTTLS; no secret is needed for automated tests. The existing API project UserSecretsId supports local configuration.

Policy terms/privacy pages remain explicitly marked drafts requiring the owner's final text. This does not introduce a membership approval process.

## Physical schema reference

The historical status names below are storage values only.

# V1 persistence contract

## Administration extension (v1.1)

The administration requirements supplied on 5 November 2026 authorize an additive extension to the original five-table schema. The current baseline SQL includes `admin_users.security_version` (integer, NOT NULL, default 0), nullable `members.category_id` (uuid), and `MembershipCategory -> membership_categories`. Categories have uuid id/tenant_id, name/normalized_name varchar(100), active boolean and UTC created/updated timestamps. Tenant/name is unique; a composite member/category foreign key enforces matching tenant ownership. Existing members require no backfill and may remain unclassified. Credential versioning revokes old administrator tokens after security changes. No existing table/column or domain status is removed.

The original catalog below describes v1.0; this section and the updated baseline define the authorized additions. The original EF migration remains unchanged. `AdministrationAndMembershipCategories` is the upgrade mechanism, with reference SQL in `IKhethelo.Digital.Api/artifacts/administration-upgrade.sql`. Do not rerun CREATE TABLE IF NOT EXISTS baseline SQL as an upgrade. AuditLog also provides the new member-history projection; no separate history or full Identity schema is added. See `IKhethelo.Digital.Api/ADMINISTRATION_IMPLEMENTATION_REPORT.md` for implemented behavior and verification.

## Authority and mappings

The baseline is `database/ikhethelo_digital_postgresql_baseline.sql`, originally supplied by the user and now extended for the authorized administration requirements above. EF Core migrations reproduce its definitions and remain the executable schema evolution/deployment mechanism. Do not independently redesign the schema.

The fixed entities are exactly Tenant -> tenants, AdminUser -> admin_users, MembershipApplication -> membership_applications, Member -> members and AuditLog -> audit_logs. The catalogs below are derived from the actual SQL, including every column, named constraint and explicitly declared index. PostgreSQL also creates indexes backing PRIMARY KEY and UNIQUE constraints; preserve those constraints rather than inventing duplicate indexes. PK index names are PostgreSQL-generated and not explicitly named by this baseline.

Each id is a non-null Guid/uuid primary key with no database UUID default. C# property names are PascalCase equivalents of these exact snake_case columns (id -> Id, tenant_id -> TenantId, application_id -> ApplicationId). Map uuid -> Guid, varchar/text -> string, boolean -> bool, date -> DateOnly, timestamptz -> UTC DateTime/DateTimeOffset, jsonb -> JSON value. Nullable SQL columns require nullable C# values. Application Status, Member Status and AdminUser Role are controlled string/enum conversions matching their CHECKs. A timestamptz represents an instant; API serialization must use UTC.

CURRENT_TIMESTAMP defaults initialize insert values only. No updated_at_utc trigger is supplied: explicitly set update timestamps on writes. Defaults do not imply nullable columns. No additional V1 tables, Identity schema, RLS policies or triggers are approved by this baseline.

## Exact column and constraint catalog

### tenants

| Column | PostgreSQL type | Nullable | Default / key |
| --- | --- | --- | --- |
| id | uuid | No | PRIMARY KEY; no default |
| code | varchar(50) | No | No default |
| name | varchar(200) | No | No default |
| is_active | boolean | No | true |
| created_at_utc | timestamptz | No | CURRENT_TIMESTAMP |
| updated_at_utc | timestamptz | No | CURRENT_TIMESTAMP |

Exact named constraints (verbatim SQL excerpt):

```sql
    CONSTRAINT uq_tenants_code UNIQUE (code),
    CONSTRAINT ck_tenants_code_not_blank CHECK (btrim(code) <> ''),
    CONSTRAINT ck_tenants_name_not_blank CHECK (btrim(name) <> '')
```

### admin_users

| Column | PostgreSQL type | Nullable | Default / key |
| --- | --- | --- | --- |
| id | uuid | No | PRIMARY KEY; no default |
| tenant_id | uuid | Yes | No default |
| email | varchar(320) | No | No default |
| normalized_email | varchar(320) | No | No default |
| password_hash | text | No | No default |
| role | varchar(50) | No | No default |
| is_active | boolean | No | true |
| created_at_utc | timestamptz | No | CURRENT_TIMESTAMP |
| updated_at_utc | timestamptz | No | CURRENT_TIMESTAMP |
| last_login_at_utc | timestamptz | Yes | No default |

Exact named constraints (verbatim SQL excerpt):

```sql
    CONSTRAINT fk_admin_users_tenant
        FOREIGN KEY (tenant_id)
        REFERENCES tenants(id)
        ON DELETE RESTRICT,

    CONSTRAINT uq_admin_users_normalized_email UNIQUE (normalized_email),

    CONSTRAINT ck_admin_users_role
        CHECK (role IN ('TenantAdmin', 'IKhetheloAdmin')),

    CONSTRAINT ck_admin_users_tenant_scope
        CHECK (
            (role = 'TenantAdmin' AND tenant_id IS NOT NULL)
            OR
            (role = 'IKhetheloAdmin' AND tenant_id IS NULL)
        )
```

### membership_applications

| Column | PostgreSQL type | Nullable | Default / key |
| --- | --- | --- | --- |
| id | uuid | No | PRIMARY KEY; no default |
| tenant_id | uuid | No | No default |
| first_name | varchar(100) | No | No default |
| last_name | varchar(100) | No | No default |
| date_of_birth | date | No | No default |
| phone_number | varchar(50) | No | No default |
| normalized_phone_number | varchar(50) | No | No default |
| email | varchar(320) | No | No default |
| normalized_email | varchar(320) | No | No default |
| status | varchar(30) | No | 'Pending' |
| terms_accepted | boolean | No | No default |
| privacy_consent | boolean | No | No default |
| submitted_at_utc | timestamptz | No | CURRENT_TIMESTAMP |
| updated_at_utc | timestamptz | No | CURRENT_TIMESTAMP |
| reviewed_at_utc | timestamptz | Yes | No default |
| reviewed_by_admin_id | uuid | Yes | No default |
| review_notes | varchar(2000) | Yes | No default |

Exact named constraints (verbatim SQL excerpt):

```sql
    CONSTRAINT fk_membership_applications_tenant
        FOREIGN KEY (tenant_id)
        REFERENCES tenants(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_membership_applications_reviewed_by
        FOREIGN KEY (reviewed_by_admin_id)
        REFERENCES admin_users(id)
        ON DELETE RESTRICT,

    CONSTRAINT ck_membership_applications_status
        CHECK (status IN ('Pending', 'Approved', 'Rejected')),

    CONSTRAINT ck_membership_applications_terms
        CHECK (terms_accepted = true),

    CONSTRAINT ck_membership_applications_privacy
        CHECK (privacy_consent = true),

    CONSTRAINT ck_membership_applications_dob
        CHECK (date_of_birth <= CURRENT_DATE),

    CONSTRAINT ck_membership_applications_names
        CHECK (
            char_length(btrim(first_name)) >= 2
            AND char_length(btrim(last_name)) >= 2
        ),

    CONSTRAINT ck_membership_applications_review_state
        CHECK (
            (
                status = 'Pending'
                AND reviewed_at_utc IS NULL
                AND reviewed_by_admin_id IS NULL
            )
            OR
            (
                status IN ('Approved', 'Rejected')
                AND reviewed_at_utc IS NOT NULL
                AND reviewed_by_admin_id IS NOT NULL
            )
        )
```

### members

| Column | PostgreSQL type | Nullable | Default / key |
| --- | --- | --- | --- |
| id | uuid | No | PRIMARY KEY; no default |
| tenant_id | uuid | No | No default |
| application_id | uuid | No | No default |
| member_number | varchar(50) | No | No default |
| first_name | varchar(100) | No | No default |
| last_name | varchar(100) | No | No default |
| date_of_birth | date | No | No default |
| phone_number | varchar(50) | No | No default |
| normalized_phone_number | varchar(50) | No | No default |
| email | varchar(320) | No | No default |
| normalized_email | varchar(320) | No | No default |
| status | varchar(30) | No | 'Active' |
| joined_at_utc | timestamptz | No | CURRENT_TIMESTAMP |
| updated_at_utc | timestamptz | No | CURRENT_TIMESTAMP |

Exact named constraints (verbatim SQL excerpt):

```sql
    CONSTRAINT fk_members_tenant
        FOREIGN KEY (tenant_id)
        REFERENCES tenants(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_members_application
        FOREIGN KEY (application_id)
        REFERENCES membership_applications(id)
        ON DELETE RESTRICT,

    CONSTRAINT uq_members_application UNIQUE (application_id),

    CONSTRAINT uq_members_tenant_member_number
        UNIQUE (tenant_id, member_number),

    CONSTRAINT ck_members_status
        CHECK (status IN ('Active', 'Suspended', 'Inactive')),

    CONSTRAINT ck_members_names
        CHECK (
            char_length(btrim(first_name)) >= 2
            AND char_length(btrim(last_name)) >= 2
        )
```

### audit_logs

| Column | PostgreSQL type | Nullable | Default / key |
| --- | --- | --- | --- |
| id | uuid | No | PRIMARY KEY; no default |
| tenant_id | uuid | Yes | No default |
| actor_admin_user_id | uuid | Yes | No default |
| action | varchar(150) | No | No default |
| entity_type | varchar(150) | No | No default |
| entity_id | uuid | Yes | No default |
| correlation_id | varchar(150) | Yes | No default |
| metadata_json | jsonb | Yes | No default |
| created_at_utc | timestamptz | No | CURRENT_TIMESTAMP |

Exact named constraints (verbatim SQL excerpt):

```sql
    CONSTRAINT fk_audit_logs_tenant
        FOREIGN KEY (tenant_id)
        REFERENCES tenants(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_audit_logs_actor
        FOREIGN KEY (actor_admin_user_id)
        REFERENCES admin_users(id)
        ON DELETE SET NULL,

    CONSTRAINT ck_audit_logs_action_not_blank
        CHECK (btrim(action) <> ''),

    CONSTRAINT ck_audit_logs_entity_type_not_blank
        CHECK (btrim(entity_type) <> '')
```

## Explicit index catalog

| Index | Table | Keys / ordering | Unique | Predicate |
| --- | --- | --- | --- | --- |
| ix_admin_users_tenant_id | admin_users | tenant_id | No | None |
| ix_membership_applications_tenant_status | membership_applications | tenant_id, status | No | None |
| ix_membership_applications_tenant_submitted | membership_applications | tenant_id, submitted_at_utc DESC | No | None |
| ix_membership_applications_tenant_email | membership_applications | tenant_id, normalized_email | No | None |
| ix_membership_applications_tenant_phone | membership_applications | tenant_id, normalized_phone_number | No | None |
| ux_membership_applications_pending_email | membership_applications | tenant_id, normalized_email | Yes | status = 'Pending' |
| ux_membership_applications_pending_phone | membership_applications | tenant_id, normalized_phone_number | Yes | status = 'Pending' |
| ix_members_tenant_status | members | tenant_id, status | No | None |
| ix_members_tenant_joined | members | tenant_id, joined_at_utc DESC | No | None |
| ix_members_tenant_email | members | tenant_id, normalized_email | No | None |
| ix_members_tenant_phone | members | tenant_id, normalized_phone_number | No | None |
| ix_members_tenant_member_number | members | tenant_id, member_number | No | None |
| ux_members_active_email | members | tenant_id, normalized_email | Yes | status = 'Active' |
| ux_members_active_phone | members | tenant_id, normalized_phone_number | Yes | status = 'Active' |
| ix_audit_logs_tenant_created | audit_logs | tenant_id, created_at_utc DESC | No | None |
| ix_audit_logs_actor_created | audit_logs | actor_admin_user_id, created_at_utc DESC | No | None |
| ix_audit_logs_entity | audit_logs | entity_type, entity_id | No | None |

## Relationships, state and application responsibilities

All tenant relationships use tenants.id. admin_users.tenant_id is nullable only for IKhetheloAdmin; TenantAdmin requires it. Administrator normalized_email is globally unique. Reviewed applications refer to admin_users.id; members.application_id refers to membership_applications.id and is globally unique, so one application can create at most one member. Tenant-owned applications/members require tenant_id. audit_logs.tenant_id, actor_admin_user_id, entity_id, correlation_id and metadata_json are nullable. All declared FK deletes are RESTRICT except audit_logs.actor_admin_user_id, which uses SET NULL. Audit rows are append-oriented by application policy; there is no SQL immutability trigger.

The baseline's foreign keys validate referenced existence, not same-tenant agreement. Application use cases must validate member/application tenant equality, reviewer's tenant authorization (or explicit internal policy), audit tenant/actor consistency, and Approved application/member consistency. These requirements must not be misrepresented as composite FKs or database CHECKs that do not exist. No hard-delete API is in V1.

Application statuses are Pending, Approved, Rejected; default Pending. Pending requires reviewed_at_utc and reviewed_by_admin_id to be NULL. Approved/Rejected require both non-null. review_notes is independently nullable and is not required by SQL. Both consents must be true. Names require at least two characters after btrim. The nonfuture birth-date CHECK uses CURRENT_DATE; preserve it, but it is not an age CHECK. Enforce 18+ in C# using the submission date in Africa/Johannesburg as documented in MASTER_SPEC.md. PostgreSQL session dates can differ around timezone boundaries; the application age rule is authoritative, and CURRENT_DATE must not become the source of applicant-age validation.

Member statuses are Active, Suspended, Inactive; default Active. HTTP member updates permit only names and contact details; the original application's snapshot is not changed. Status transitions between the three declared values are application policy, with Active identity conflict checks. When a Member is created, copy the approved application fields and both normalized contacts. Name validation matches the SQL minimum and maximum; email max 320 and review notes max 2000 match persistence. HTTP canonical E.164 phone validation (max 16) is intentionally stricter than SQL varchar(50). SQL does not validate email/phone format or implement normalization; the application must do so consistently before all writes and comparisons.

The two Pending partial unique indexes independently prevent either normalized email OR normalized phone duplicates within the same tenant. The two Active-member partial unique indexes independently prevent either normalized identity collision within the same tenant. These rules do not cross tenant boundaries and do not constrain Suspended/Inactive members. Reactivation and contact edits of Active members must check conflicts and translate racing unique violations into safe 409 responses. The member-number index explicitly declared in SQL overlaps the tenant/member-number UNIQUE constraint; preserve both as approved rather than silently optimizing the blueprint.

## Member-number sequence and transactions

member_number_seq is bigint, START 1, INCREMENT 1, MINVALUE 1, NO MAXVALUE, CACHE 1. No cycle or OWNED BY clause is supplied; default is NO CYCLE. It is a global sequence, not a separate per-tenant counter. Allocate with nextval, never MAX+1. Es'hlahleni formats ESC- plus a minimum of five digits (1 -> ESC-00001); larger values must not be truncated. Other tenants may eventually use another display prefix through an intentional business rule. Tenant/member-number uniqueness remains mandatory. Sequence allocations are not rolled back; gaps are acceptable.

Approval is one transaction: load the application under authorized tenant scope and concurrency protection; require Pending; recheck Active-member identity conflicts; allocate sequence; insert exactly one Active Member; set Approved and review metadata/update timestamp; insert required AuditLog; commit. Roll back every required transactional change on failure. Conditional state updates/locking plus uq_members_application prevent competing approvals/rejections from both succeeding. Incompatible/repeated review returns 409. Rejection atomically updates review metadata and audit without creating a member. Member edits/status changes and their required audit also commit together.

## Seed and deployment verification

The script contains one seed: UUID e5a11111-5c1a-4e5a-9e5a-111111111111, code ESHL, name Es'hlahleni Social Club, active true, created/updated CURRENT_TIMESTAMP. ON CONFLICT (code) updates name, active state and updated timestamp, preserving an existing row's UUID and created timestamp. Thus the fixed UUID applies to a fresh database; tenant resolution must use code rather than assume that UUID on every existing installation. The unique code and upsert make repeated seeding idempotent in row count, but not timestamp-stable. Do not seed applicants or passwords.

BEGIN/COMMIT groups the reference script. CREATE IF NOT EXISTS does not inspect/repair an existing divergent table, constraint, index or sequence; the baseline is not an upgrade tool. Later EF migrations must reproduce its schema and seed behavior. The closing SQL comments discuss a possible full Identity switch; they do not override the master requirement for an intentional architecture/baseline revision first.

No SQL has been executed in this pass. Later verification must compare generated migration SQL and actual PostgreSQL catalogs for every column, default, nullability, PK/FK action, CHECK, unique constraint, index/predicate, sequence and seed. Run meaningful PostgreSQL integration tests for tenant relationships, duplicate races, atomic approval/rollback, double review and exactly one member per application. Contract reconciliation is complete; runtime/schema deployment verification remains a later phase.
