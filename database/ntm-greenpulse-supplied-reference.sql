
-- ============================================================
-- NTM GREENPULSE NETWORK
-- Additive PostgreSQL schema for iKhethelo Digital Ecosystem
-- ============================================================
-- IMPORTANT:
-- 1. Review against the existing EF Core entity mappings.
-- 2. Assumes tenants.id is UUID.
-- 3. Do not run directly AND apply an equivalent EF migration.
-- 4. Verify original workbook headings before production use.
-- 5. Existing tenants/admin_users/audit_logs are preserved.
-- ============================================================

BEGIN;

-- ------------------------------------------------------------
-- 1. VETERANS
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS ntm_veterans (
    id UUID PRIMARY KEY,

    tenant_id UUID NOT NULL,

    full_name VARCHAR(200) NOT NULL,

    mkmva_number VARCHAR(100),
    dmv_number VARCHAR(100),

    contact_number VARCHAR(50),

    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_ntm_veterans_tenant
        FOREIGN KEY (tenant_id)
        REFERENCES tenants(id)
        ON DELETE RESTRICT,

    CONSTRAINT ck_ntm_veterans_full_name
        CHECK (LENGTH(BTRIM(full_name)) > 0),

    CONSTRAINT ck_ntm_veterans_registration
        CHECK (
            NULLIF(BTRIM(mkmva_number), '') IS NOT NULL
            OR
            NULLIF(BTRIM(dmv_number), '') IS NOT NULL
        ),

    CONSTRAINT uq_ntm_veterans_tenant_id
        UNIQUE (tenant_id, id)
);

-- Duplicate registration numbers are not allowed
-- within the same tenant.

CREATE UNIQUE INDEX IF NOT EXISTS
    ux_ntm_veterans_mkmva
ON ntm_veterans (
    tenant_id,
    UPPER(BTRIM(mkmva_number))
)
WHERE NULLIF(BTRIM(mkmva_number), '') IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS
    ux_ntm_veterans_dmv
ON ntm_veterans (
    tenant_id,
    UPPER(BTRIM(dmv_number))
)
WHERE NULLIF(BTRIM(dmv_number), '') IS NOT NULL;

CREATE INDEX IF NOT EXISTS
    ix_ntm_veterans_tenant_name
ON ntm_veterans (tenant_id, full_name);


-- ------------------------------------------------------------
-- 2. BUSINESSES AND NEEDS ASSESSMENT
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS ntm_businesses (
    id UUID PRIMARY KEY,

    tenant_id UUID NOT NULL,
    veteran_id UUID NOT NULL,

    -- Business identification
    business_name VARCHAR(200) NOT NULL,
    cipc_registration_number VARCHAR(100),
    business_address TEXT,
    sector VARCHAR(200),
    business_background TEXT,

    -- Operational information
    operational_status VARCHAR(30),
    number_of_employees INTEGER,
    years_in_operation NUMERIC(6,1),

    -- Needs assessment
    -- Descriptive text is preserved instead of forcing
    -- unverified spreadsheet values into booleans.
    current_business_needs TEXT,
    priority_need TEXT,

    -- Funding
    funding_required BOOLEAN,
    funding_amount NUMERIC(15,2),

    -- Assessment metadata
    additional_comments TEXT,
    assessment_date DATE,
    assessed_by VARCHAR(200),

    -- Audit timestamps
    created_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at_utc TIMESTAMPTZ NOT NULL DEFAULT NOW(),

    CONSTRAINT fk_ntm_businesses_tenant
        FOREIGN KEY (tenant_id)
        REFERENCES tenants(id)
        ON DELETE RESTRICT,

    CONSTRAINT fk_ntm_businesses_veteran
        FOREIGN KEY (tenant_id, veteran_id)
        REFERENCES ntm_veterans(tenant_id, id)
        ON DELETE RESTRICT,

    -- Confirmed rule: one business per veteran.
    CONSTRAINT uq_ntm_businesses_tenant_veteran
        UNIQUE (tenant_id, veteran_id),

    CONSTRAINT ck_ntm_businesses_name
        CHECK (LENGTH(BTRIM(business_name)) > 0),

    CONSTRAINT ck_ntm_businesses_status
        CHECK (
            operational_status IS NULL
            OR operational_status IN (
                'Operational',
                'Partially Operational',
                'Not Operational'
            )
        ),

    CONSTRAINT ck_ntm_businesses_employees
        CHECK (
            number_of_employees IS NULL
            OR number_of_employees >= 0
        ),

    CONSTRAINT ck_ntm_businesses_years
        CHECK (
            years_in_operation IS NULL
            OR years_in_operation >= 0
        ),

    CONSTRAINT ck_ntm_businesses_funding
        CHECK (
            funding_amount IS NULL
            OR funding_amount >= 0
        )
);

CREATE INDEX IF NOT EXISTS
    ix_ntm_businesses_tenant_name
ON ntm_businesses (tenant_id, business_name);

CREATE INDEX IF NOT EXISTS
    ix_ntm_businesses_tenant_sector
ON ntm_businesses (tenant_id, sector);

CREATE INDEX IF NOT EXISTS
    ix_ntm_businesses_tenant_status
ON ntm_businesses (tenant_id, operational_status);

CREATE INDEX IF NOT EXISTS
    ix_ntm_businesses_tenant_assessment_date
ON ntm_businesses (tenant_id, assessment_date);


-- ------------------------------------------------------------
-- 3. UPDATED TIMESTAMP TRIGGER
-- NTM-specific name avoids collisions with existing functions.
-- ------------------------------------------------------------

CREATE OR REPLACE FUNCTION
    ntm_set_updated_at_utc()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at_utc = NOW();
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS
    trg_ntm_veterans_updated_at
ON ntm_veterans;

CREATE TRIGGER trg_ntm_veterans_updated_at
BEFORE UPDATE ON ntm_veterans
FOR EACH ROW
EXECUTE FUNCTION ntm_set_updated_at_utc();

DROP TRIGGER IF EXISTS
    trg_ntm_businesses_updated_at
ON ntm_businesses;

CREATE TRIGGER trg_ntm_businesses_updated_at
BEFORE UPDATE ON ntm_businesses
FOR EACH ROW
EXECUTE FUNCTION ntm_set_updated_at_utc();


COMMIT;

-- ============================================================
-- IMPORTANT IMPLEMENTATION NOTES
-- ============================================================
-- Tenant seed:
-- Create the NTM tenant using the existing tenant provisioning
-- mechanism, not a guessed tenant ID or guessed column names.
--
-- Admins:
-- Reuse admin_users, JWT authentication and admin management.
--
-- Auditing:
-- Reuse the existing audit_logs implementation.
--
-- Needs:
-- Compare current_business_needs, priority_need,
-- funding_required and funding_amount with the workbook.
-- Add or adjust fields only when the source requires it.
--
-- Migration:
-- Prefer an equivalent EF Core migration for the actual app.
-- ============================================================
