-- =====================================================================
-- iKhethelo Digital API - PostgreSQL Baseline Schema
-- Es'hlahleni Social Club is the first tenant (ESHL)
--
-- Purpose:
--   Concrete PostgreSQL reference/baseline schema derived from the agreed
--   iKhethelo Digital master architecture, API contract, and Es'hlahleni
--   membership requirements.
--
-- Important:
--   EF Core migrations remain the authoritative production schema mechanism.
--   The ASP.NET Core EF model/migrations should be made to match this schema
--   (or this script regenerated from the final migrations) before deployment.
-- =====================================================================

BEGIN;

-- ---------------------------------------------------------------------
-- 1. Tenants
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS tenants (
    id                  uuid PRIMARY KEY,
    code                varchar(50)  NOT NULL,
    name                varchar(200) NOT NULL,
    is_active           boolean      NOT NULL DEFAULT true,
    created_at_utc      timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc      timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT uq_tenants_code UNIQUE (code),
    CONSTRAINT ck_tenants_code_not_blank CHECK (btrim(code) <> ''),
    CONSTRAINT ck_tenants_name_not_blank CHECK (btrim(name) <> '')
);

-- ---------------------------------------------------------------------
-- 2. Administrative users
--
-- MVP role model:
--   TenantAdmin     -> scoped to exactly one tenant
--   IKhetheloAdmin  -> internal iKhethelo Digital cross-tenant administrator
--
-- No password is seeded by this script.
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS admin_users (
    id                  uuid PRIMARY KEY,
    tenant_id           uuid NULL,
    email               varchar(320) NOT NULL,
    normalized_email    varchar(320) NOT NULL,
    password_hash       text         NOT NULL,
    security_version    integer      NOT NULL DEFAULT 0,
    role                varchar(50)  NOT NULL,
    is_active           boolean      NOT NULL DEFAULT true,
    created_at_utc      timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc      timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    last_login_at_utc   timestamptz  NULL,

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
);

CREATE INDEX IF NOT EXISTS ix_admin_users_tenant_id
    ON admin_users (tenant_id);

-- ---------------------------------------------------------------------
-- 3. Membership applications
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS membership_applications (
    id                          uuid PRIMARY KEY,
    tenant_id                   uuid         NOT NULL,
    first_name                  varchar(100) NOT NULL,
    last_name                   varchar(100) NOT NULL,
    date_of_birth               date         NOT NULL,
    phone_number                varchar(50)  NOT NULL,
    normalized_phone_number     varchar(50)  NOT NULL,
    email                       varchar(320) NOT NULL,
    normalized_email            varchar(320) NOT NULL,
    status                      varchar(30)  NOT NULL DEFAULT 'Pending',
    terms_accepted              boolean      NOT NULL,
    privacy_consent             boolean      NOT NULL,
    submitted_at_utc            timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc              timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    reviewed_at_utc             timestamptz  NULL,
    reviewed_by_admin_id        uuid         NULL,
    review_notes                varchar(2000) NULL,

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
);

CREATE INDEX IF NOT EXISTS ix_membership_applications_tenant_status
    ON membership_applications (tenant_id, status);

CREATE INDEX IF NOT EXISTS ix_membership_applications_tenant_submitted
    ON membership_applications (tenant_id, submitted_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_membership_applications_tenant_email
    ON membership_applications (tenant_id, normalized_email);

CREATE INDEX IF NOT EXISTS ix_membership_applications_tenant_phone
    ON membership_applications (tenant_id, normalized_phone_number);

-- Prevent duplicate Pending applications within the SAME tenant.
CREATE UNIQUE INDEX IF NOT EXISTS ux_membership_applications_pending_email
    ON membership_applications (tenant_id, normalized_email)
    WHERE status = 'Pending';

CREATE UNIQUE INDEX IF NOT EXISTS ux_membership_applications_pending_phone
    ON membership_applications (tenant_id, normalized_phone_number)
    WHERE status = 'Pending';

-- ---------------------------------------------------------------------
-- 4. Member-number sequence
--
-- Concurrency-safe. Sequence gaps after rollbacks are acceptable.
-- The API formats the returned numeric value for Es'hlahleni as:
--   ESC-00001, ESC-00002, ...
--
-- Future tenants may use different display prefixes.
-- ---------------------------------------------------------------------
CREATE SEQUENCE IF NOT EXISTS member_number_seq
    AS bigint
    START WITH 1
    INCREMENT BY 1
    MINVALUE 1
    NO MAXVALUE
    CACHE 1;

-- Tenant-defined membership types/categories. Existing members may remain unclassified.
CREATE TABLE IF NOT EXISTS membership_categories (
    id                  uuid PRIMARY KEY,
    tenant_id           uuid NOT NULL,
    name                varchar(100) NOT NULL,
    normalized_name     varchar(100) NOT NULL,
    is_active           boolean NOT NULL DEFAULT true,
    created_at_utc      timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc      timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT uq_membership_categories_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT fk_membership_categories_tenant FOREIGN KEY (tenant_id) REFERENCES tenants(id) ON DELETE RESTRICT,
    CONSTRAINT ck_membership_categories_name CHECK (char_length(btrim(name)) BETWEEN 2 AND 100)
);
CREATE UNIQUE INDEX IF NOT EXISTS ux_membership_categories_tenant_name
    ON membership_categories (tenant_id, normalized_name);

-- ---------------------------------------------------------------------
-- 5. Members
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS members (
    id                          uuid PRIMARY KEY,
    tenant_id                   uuid         NOT NULL,
    application_id              uuid         NOT NULL,
    category_id                 uuid         NULL,
    member_number               varchar(50)  NOT NULL,
    first_name                  varchar(100) NOT NULL,
    last_name                   varchar(100) NOT NULL,
    date_of_birth               date         NOT NULL,
    phone_number                varchar(50)  NOT NULL,
    normalized_phone_number     varchar(50)  NOT NULL,
    email                       varchar(320) NOT NULL,
    normalized_email            varchar(320) NOT NULL,
    status                      varchar(30)  NOT NULL DEFAULT 'Active',
    joined_at_utc               timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at_utc              timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT fk_members_tenant
        FOREIGN KEY (tenant_id)
        REFERENCES tenants(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_members_application
        FOREIGN KEY (application_id)
        REFERENCES membership_applications(id)
        ON DELETE RESTRICT,

    CONSTRAINT uq_members_application UNIQUE (application_id),

    CONSTRAINT fk_members_category_tenant FOREIGN KEY (tenant_id, category_id)
        REFERENCES membership_categories(tenant_id, id) ON DELETE RESTRICT,

    CONSTRAINT uq_members_tenant_member_number
        UNIQUE (tenant_id, member_number),

    CONSTRAINT ck_members_status
        CHECK (status IN ('Active', 'Suspended', 'Inactive')),

    CONSTRAINT ck_members_names
        CHECK (
            char_length(btrim(first_name)) >= 2
            AND char_length(btrim(last_name)) >= 2
        )
);

CREATE INDEX IF NOT EXISTS ix_members_tenant_status
    ON members (tenant_id, status);

CREATE INDEX IF NOT EXISTS ix_members_tenant_joined
    ON members (tenant_id, joined_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_members_tenant_email
    ON members (tenant_id, normalized_email);

CREATE INDEX IF NOT EXISTS ix_members_tenant_phone
    ON members (tenant_id, normalized_phone_number);

CREATE INDEX IF NOT EXISTS ix_members_tenant_member_number
    ON members (tenant_id, member_number);

-- The agreed MVP treats an existing Active member with the same normalized
-- email or phone number inside the same tenant as a business conflict.
CREATE UNIQUE INDEX IF NOT EXISTS ux_members_active_email
    ON members (tenant_id, normalized_email)
    WHERE status = 'Active';

CREATE UNIQUE INDEX IF NOT EXISTS ux_members_active_phone
    ON members (tenant_id, normalized_phone_number)
    WHERE status = 'Active';

-- ---------------------------------------------------------------------
-- 6. Audit logs
-- ---------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS audit_logs (
    id                  uuid PRIMARY KEY,
    tenant_id           uuid NULL,
    actor_admin_user_id uuid NULL,
    action              varchar(150) NOT NULL,
    entity_type         varchar(150) NOT NULL,
    entity_id           uuid NULL,
    correlation_id      varchar(150) NULL,
    metadata_json       jsonb NULL,
    created_at_utc      timestamptz NOT NULL DEFAULT CURRENT_TIMESTAMP,

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
);

CREATE INDEX IF NOT EXISTS ix_audit_logs_tenant_created
    ON audit_logs (tenant_id, created_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_audit_logs_actor_created
    ON audit_logs (actor_admin_user_id, created_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_audit_logs_entity
    ON audit_logs (entity_type, entity_id);

-- ---------------------------------------------------------------------
-- 7. Seed first tenant: Es'hlahleni Social Club
--
-- Stable seed UUID for the initial tenant. The API should resolve the tenant
-- by Code = ESHL, not by exposing/trusting this UUID in the browser.
-- ---------------------------------------------------------------------
INSERT INTO tenants (
    id,
    code,
    name,
    is_active,
    created_at_utc,
    updated_at_utc
)
VALUES (
    'e5a11111-5c1a-4e5a-9e5a-111111111111',
    'ESHL',
    'Es''hlahleni Social Club',
    true,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
)
ON CONFLICT (code) DO UPDATE
SET
    name = EXCLUDED.name,
    is_active = EXCLUDED.is_active,
    updated_at_utc = CURRENT_TIMESTAMP;

COMMIT;

-- =====================================================================
-- Notes for the ASP.NET Core / EF Core implementation
-- =====================================================================
--
-- 1. Public Angular requests must NEVER send/trust database TenantId.
--    The API resolves ESHL from a trusted tenant code/route/context.
--
-- 2. Applicant age >= 18 must be validated authoritatively in ASP.NET Core.
--    It is intentionally not encoded as a time-dependent PostgreSQL CHECK.
--
-- 3. On approval, use one DB transaction:
--       - verify application is still Pending
--       - obtain nextval('member_number_seq')
--       - format Es'hlahleni number as ESC-00001, etc.
--       - create Member
--       - set application to Approved
--       - set review fields
--       - write required audit record
--       - commit
--
-- 4. uq_members_application plus the transaction protects against duplicate
--    members from the same application. The API should still return a clean
--    409 Conflict for racing/double approval attempts.
--
-- 5. admin_users is a deliberately small MVP identity schema. If the final
--    API implementation chooses full ASP.NET Core Identity instead, replace
--    this table with the Identity schema and update all related FKs/migrations.
--
-- 6. EF Core migrations remain authoritative for production. This SQL file is
--    the agreed concrete database baseline/reference and should be kept aligned.
-- =====================================================================
