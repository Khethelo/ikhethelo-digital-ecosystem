START TRANSACTION;
ALTER TABLE members ADD category_id uuid;

ALTER TABLE admin_users ADD security_version integer NOT NULL DEFAULT 0;

CREATE TABLE membership_categories (
    id uuid NOT NULL,
    tenant_id uuid NOT NULL,
    name character varying(100) NOT NULL,
    normalized_name character varying(100) NOT NULL,
    is_active boolean NOT NULL DEFAULT TRUE,
    created_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    updated_at_utc timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT membership_categories_pkey PRIMARY KEY (id),
    CONSTRAINT uq_membership_categories_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT ck_membership_categories_name CHECK (char_length(btrim(name)) BETWEEN 2 AND 100),
    CONSTRAINT fk_membership_categories_tenant FOREIGN KEY (tenant_id) REFERENCES tenants (id) ON DELETE RESTRICT
);

CREATE UNIQUE INDEX ux_membership_categories_tenant_name ON membership_categories (tenant_id, normalized_name);

ALTER TABLE members ADD CONSTRAINT fk_members_category_tenant FOREIGN KEY (tenant_id, category_id) REFERENCES membership_categories (tenant_id, id) ON DELETE RESTRICT;

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261105154301_AdministrationAndMembershipCategories', '10.0.14');

COMMIT;

