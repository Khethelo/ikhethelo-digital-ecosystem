# Tenant staff deployment

`20261119231544_TenantStaffRoles` replaces two check constraints on `admin_users`: it permits `TenantStaff`, and requires that role to have a tenant ID. It updates no account rows and retains the string values and numeric enum values of existing roles. PostgreSQL takes a table lock while changing and validating constraints. Existing `TenantAdmin` and `IKhetheloAdmin` accounts retain their roles and passwords. No bootstrap or seed runs.

Review the target, existing tenant administrators, migration SQL, backup and rollback plan before applying it. The migration must be applied before enabling creation of staff accounts; rolling back will fail while any `TenantStaff` rows remain, because the old constraints reject them. Do not silently convert or delete accounts to make a rollback succeed.

The API and frontend commit does not apply migrations automatically. Render and Netlify deploy independently, so coordinate migration and deployment to avoid exposing a staff-creation form before the database accepts the role. Existing account assignments must be reviewed individually and are not changed by deployment. The new role is authorized for the NTM operational policy only; it does not grant access to membership or user-management policies. Existing API operations define the permitted mutations; this change does not introduce record deletion endpoints.
