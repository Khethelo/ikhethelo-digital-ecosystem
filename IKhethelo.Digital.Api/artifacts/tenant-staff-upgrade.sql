START TRANSACTION;
ALTER TABLE admin_users DROP CONSTRAINT ck_admin_users_role;

ALTER TABLE admin_users DROP CONSTRAINT ck_admin_users_tenant_scope;

ALTER TABLE admin_users ADD CONSTRAINT ck_admin_users_role CHECK (role IN ('TenantAdmin', 'TenantStaff', 'IKhetheloAdmin'));

ALTER TABLE admin_users ADD CONSTRAINT ck_admin_users_tenant_scope CHECK ((role IN ('TenantAdmin', 'TenantStaff') AND tenant_id IS NOT NULL) OR (role = 'IKhetheloAdmin' AND tenant_id IS NULL));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261119231544_TenantStaffRoles', '10.0.14');

COMMIT;
