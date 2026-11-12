CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;
CREATE SEQUENCE member_number_seq START WITH 1 INCREMENT BY 1 MINVALUE 1 NO CYCLE;

CREATE TABLE tenants (
    id uuid NOT NULL,
    code character varying(50) NOT NULL,
    name character varying(200) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT tenants_pkey PRIMARY KEY (id),
    CONSTRAINT uq_tenants_code UNIQUE (code),
    CONSTRAINT ck_tenants_code_not_blank CHECK (btrim(code) <> ''),
    CONSTRAINT ck_tenants_name_not_blank CHECK (btrim(name) <> '')
);

CREATE TABLE admin_users (
    id uuid NOT NULL,
    tenant_id uuid,
    email character varying(320) NOT NULL,
    normalized_email character varying(320) NOT NULL,
    password_hash text NOT NULL,
    role character varying(50) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    last_login_at_utc timestamp with time zone,
    CONSTRAINT admin_users_pkey PRIMARY KEY (id),
    CONSTRAINT uq_admin_users_normalized_email UNIQUE (normalized_email),
    CONSTRAINT ck_admin_users_role CHECK (role IN ('TenantAdmin', 'IKhetheloAdmin')),
    CONSTRAINT ck_admin_users_tenant_scope CHECK ((role = 'TenantAdmin' AND tenant_id IS NOT NULL) OR (role = 'IKhetheloAdmin' AND tenant_id IS NULL)),
    CONSTRAINT fk_admin_users_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE RESTRICT
);

CREATE TABLE audit_logs (
    id uuid NOT NULL,
    tenant_id uuid,
    actor_admin_user_id uuid,
    action character varying(150) NOT NULL,
    entity_type character varying(150) NOT NULL,
    entity_id uuid,
    correlation_id character varying(150),
    metadata_json jsonb,
    created_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT audit_logs_pkey PRIMARY KEY (id),
    CONSTRAINT ck_audit_logs_action_not_blank CHECK (btrim(action) <> ''),
    CONSTRAINT ck_audit_logs_entity_type_not_blank CHECK (btrim(entity_type) <> ''),
    CONSTRAINT fk_audit_logs_actor FOREIGN KEY (actor_admin_user_id) REFERENCES admin_users (id) ON DELETE SET NULL,
    CONSTRAINT fk_audit_logs_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE RESTRICT
);

CREATE TABLE membership_applications (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    first_name character varying(100) NOT NULL,
    last_name character varying(100) NOT NULL,
    date_of_birth date NOT NULL,
    phone_number character varying(50) NOT NULL,
    normalized_phone_number character varying(50) NOT NULL,
    email character varying(320) NOT NULL,
    normalized_email character varying(320) NOT NULL,
    status character varying(30) NOT NULL DEFAULT 'Pending',
    terms_accepted boolean NOT NULL,
    privacy_consent boolean NOT NULL,
    submitted_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    reviewed_at_utc timestamp with time zone,
    reviewed_by_admin_id uuid,
    review_notes character varying(2000),
    CONSTRAINT membership_applications_pkey PRIMARY KEY (id),
    CONSTRAINT ck_membership_applications_dob CHECK (date_of_birth <= CURRENT_DATE),
    CONSTRAINT ck_membership_applications_names CHECK (char_length(btrim(first_name)) >= 2 AND char_length(btrim(last_name)) >= 2),
    CONSTRAINT ck_membership_applications_privacy CHECK (privacy_consent = true),
    CONSTRAINT ck_membership_applications_review_state CHECK ((status = 'Pending' AND reviewed_at_utc IS NULL AND reviewed_by_admin_id IS NULL) OR (status IN ('Approved', 'Rejected') AND reviewed_at_utc IS NOT NULL AND reviewed_by_admin_id IS NOT NULL)),
    CONSTRAINT ck_membership_applications_status CHECK (status IN ('Pending', 'Approved', 'Rejected')),
    CONSTRAINT ck_membership_applications_terms CHECK (terms_accepted = true),
    CONSTRAINT fk_membership_applications_reviewed_by FOREIGN KEY (reviewed_by_admin_id) REFERENCES admin_users (id) ON DELETE RESTRICT,
    CONSTRAINT fk_membership_applications_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE RESTRICT
);

CREATE TABLE members (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    application_id uuid NOT NULL,
    member_number character varying(50) NOT NULL,
    first_name character varying(100) NOT NULL,
    last_name character varying(100) NOT NULL,
    date_of_birth date NOT NULL,
    phone_number character varying(50) NOT NULL,
    normalized_phone_number character varying(50) NOT NULL,
    email character varying(320) NOT NULL,
    normalized_email character varying(320) NOT NULL,
    status character varying(30) NOT NULL DEFAULT 'Active',
    joined_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT members_pkey PRIMARY KEY (id),
    CONSTRAINT uq_members_application UNIQUE (application_id),
    CONSTRAINT uq_members_tenant_member_number UNIQUE (tenant_id, member_number),
    CONSTRAINT ck_members_names CHECK (char_length(btrim(first_name)) >= 2 AND char_length(btrim(last_name)) >= 2),
    CONSTRAINT ck_members_status CHECK (status IN ('Active', 'Suspended', 'Inactive')),
    CONSTRAINT fk_members_application FOREIGN KEY (application_id) REFERENCES membership_applications (id) ON DELETE RESTRICT,
    CONSTRAINT fk_members_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE RESTRICT
);

CREATE INDEX ix_admin_users_tenant_id ON admin_users (tenant_id);

CREATE INDEX ix_audit_logs_actor_created ON audit_logs (actor_admin_user_id, created_at_utc DESC);

CREATE INDEX ix_audit_logs_entity ON audit_logs (entity_type, entity_id);

CREATE INDEX ix_audit_logs_tenant_created ON audit_logs (tenant_id, created_at_utc DESC);

CREATE INDEX ix_members_tenant_email ON members (tenant_id, normalized_email);

CREATE INDEX ix_members_tenant_joined ON members (tenant_id, joined_at_utc DESC);

CREATE INDEX ix_members_tenant_member_number ON members (tenant_id, member_number);

CREATE INDEX ix_members_tenant_phone ON members (tenant_id, normalized_phone_number);

CREATE INDEX ix_members_tenant_status ON members (tenant_id, status);

CREATE UNIQUE INDEX ux_members_active_email ON members (tenant_id, normalized_email) WHERE status = 'Active';

CREATE UNIQUE INDEX ux_members_active_phone ON members (tenant_id, normalized_phone_number) WHERE status = 'Active';

CREATE INDEX ix_membership_applications_tenant_email ON membership_applications (tenant_id, normalized_email);

CREATE INDEX ix_membership_applications_tenant_phone ON membership_applications (tenant_id, normalized_phone_number);

CREATE INDEX ix_membership_applications_tenant_status ON membership_applications (tenant_id, status);

CREATE INDEX ix_membership_applications_tenant_submitted ON membership_applications (tenant_id, submitted_at_utc DESC);

CREATE UNIQUE INDEX ux_membership_applications_pending_email ON membership_applications (tenant_id, normalized_email) WHERE status = 'Pending';

CREATE UNIQUE INDEX ux_membership_applications_pending_phone ON membership_applications (tenant_id, normalized_phone_number) WHERE status = 'Pending';

INSERT INTO tenants (id, code, name, is_active, created_at_utc, updated_at_utc)
VALUES ('e5a11111-5c1a-4e5a-9e5a-111111111111', 'ESHL', 'Es''hlahleni Social Club', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT (code) DO UPDATE
SET name = EXCLUDED.name, is_active = EXCLUDED.is_active, updated_at_utc = CURRENT_TIMESTAMP;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261105005230_InitialBaseline', '10.0.14');

COMMIT;

