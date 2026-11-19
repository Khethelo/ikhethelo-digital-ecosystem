using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IKhethelo.Digital.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TenantStaffRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_admin_users_role",
                table: "admin_users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_admin_users_tenant_scope",
                table: "admin_users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admin_users_role",
                table: "admin_users",
                sql: "role IN ('TenantAdmin', 'TenantStaff', 'IKhetheloAdmin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admin_users_tenant_scope",
                table: "admin_users",
                sql: "(role IN ('TenantAdmin', 'TenantStaff') AND tenant_id IS NOT NULL) OR (role = 'IKhetheloAdmin' AND tenant_id IS NULL)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_admin_users_role",
                table: "admin_users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_admin_users_tenant_scope",
                table: "admin_users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admin_users_role",
                table: "admin_users",
                sql: "role IN ('TenantAdmin', 'IKhetheloAdmin')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_admin_users_tenant_scope",
                table: "admin_users",
                sql: "(role = 'TenantAdmin' AND tenant_id IS NOT NULL) OR (role = 'IKhetheloAdmin' AND tenant_id IS NULL)");
        }
    }
}
