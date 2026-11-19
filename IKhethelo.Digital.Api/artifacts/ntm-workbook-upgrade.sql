START TRANSACTION;
CREATE TABLE ntm_veterans (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    full_name character varying(200) NOT NULL,
    mkmva_number character varying(100),
    dmv_number character varying(100),
    contact_number character varying(50),
    created_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    normalized_dmv_number character varying(100) GENERATED ALWAYS AS (nullif(upper(btrim(dmv_number)), '')) STORED,
    normalized_mkmva_number character varying(100) GENERATED ALWAYS AS (nullif(upper(btrim(mkmva_number)), '')) STORED,
    CONSTRAINT ntm_veterans_pkey PRIMARY KEY (id),
    CONSTRAINT uq_ntm_veterans_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT ck_ntm_veterans_name CHECK (btrim(full_name) <> ''),
    CONSTRAINT ck_ntm_veterans_registration CHECK (nullif(btrim(mkmva_number), '') IS NOT NULL OR nullif(btrim(dmv_number), '') IS NOT NULL),
    CONSTRAINT fk_ntm_veterans_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE RESTRICT
);

CREATE TABLE ntm_businesses (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    veteran_id uuid NOT NULL,
    business_name character varying(200) NOT NULL,
    cipc_registration_number character varying(100),
    business_address text,
    sector character varying(200),
    business_background text,
    operational_status character varying(100),
    number_of_employees integer,
    years_in_operation numeric,
    current_business_needs text,
    priority_need text,
    funding_required boolean,
    funding_amount numeric,
    additional_comments text,
    assessment_date date,
    assessed_by character varying(200),
    created_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT ntm_businesses_pkey PRIMARY KEY (id),
    CONSTRAINT ck_ntm_businesses_employees CHECK (number_of_employees >= 0),
    CONSTRAINT ck_ntm_businesses_funding CHECK (funding_amount >= 0),
    CONSTRAINT ck_ntm_businesses_name CHECK (btrim(business_name) <> ''),
    CONSTRAINT ck_ntm_businesses_status CHECK (operational_status IN ('Operational', 'Partially Operational', 'Not Operational')),
    CONSTRAINT ck_ntm_businesses_years CHECK (years_in_operation >= 0),
    CONSTRAINT fk_ntm_businesses_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE RESTRICT,
    CONSTRAINT fk_ntm_businesses_veteran_tenant FOREIGN KEY (tenant_id, veteran_id) REFERENCES ntm_veterans (tenant_id, id) ON DELETE RESTRICT
);

CREATE INDEX ix_ntm_businesses_tenant_sector ON ntm_businesses (tenant_id, sector);

CREATE INDEX ix_ntm_businesses_tenant_status ON ntm_businesses (tenant_id, operational_status);

CREATE UNIQUE INDEX ux_ntm_businesses_tenant_veteran ON ntm_businesses (tenant_id, veteran_id);

CREATE UNIQUE INDEX ux_ntm_veterans_tenant_dmv ON ntm_veterans (tenant_id, normalized_dmv_number) WHERE normalized_dmv_number IS NOT NULL;

CREATE UNIQUE INDEX ux_ntm_veterans_tenant_mkmva ON ntm_veterans (tenant_id, normalized_mkmva_number) WHERE normalized_mkmva_number IS NOT NULL;

INSERT INTO tenants (id, code, name, is_active, created_at_utc, updated_at_utc)
VALUES (gen_random_uuid(), 'NTM', 'NTM GreenPulse Network', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
ON CONFLICT (code) DO NOTHING;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261112205247_NtmGreenPulseProvisional', '10.0.14');

COMMIT;

START TRANSACTION;
LOCK TABLE ntm_businesses IN ACCESS EXCLUSIVE MODE;
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM ntm_businesses
        WHERE years_in_operation > 99999.9 OR years_in_operation <> trunc(years_in_operation, 1)
           OR funding_amount > 9999999999999.99 OR funding_amount <> trunc(funding_amount, 2)) THEN
        RAISE EXCEPTION 'NTM numeric values require explicit reconciliation before workbook alignment.'
            USING ERRCODE = '23514';
    END IF;
END $$;

ALTER TABLE ntm_veterans ADD email_address character varying(320);

ALTER TABLE ntm_veterans ADD membership_reference character varying(100);

ALTER TABLE ntm_businesses ALTER COLUMN years_in_operation TYPE numeric(6,1);

ALTER TABLE ntm_businesses ALTER COLUMN operational_status TYPE character varying(30);

ALTER TABLE ntm_businesses ALTER COLUMN funding_amount TYPE numeric(15,2);

ALTER TABLE ntm_businesses ADD funding_required_source text;

ALTER TABLE ntm_businesses ADD number_of_employees_source text;

ALTER TABLE ntm_businesses ADD operational_status_source text;

ALTER TABLE ntm_businesses ADD source_record_number integer;

ALTER TABLE ntm_businesses ADD years_in_operation_source text;

CREATE INDEX ix_ntm_veterans_tenant_name ON ntm_veterans (tenant_id, full_name);

CREATE INDEX ix_ntm_businesses_tenant_assessment_date ON ntm_businesses (tenant_id, assessment_date);

CREATE INDEX ix_ntm_businesses_tenant_name ON ntm_businesses (tenant_id, business_name);

ALTER TABLE ntm_businesses ADD CONSTRAINT ck_ntm_businesses_source_record CHECK (source_record_number > 0);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261112210948_NtmWorkbookAlignment', '10.0.14');

COMMIT;

