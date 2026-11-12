using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IKhethelo.Digital.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdministrationAndMembershipCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                table: "members",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "security_version",
                table: "admin_users",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "membership_categories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("membership_categories_pkey", x => x.id);
                    table.UniqueConstraint("uq_membership_categories_tenant_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_membership_categories_name", "char_length(btrim(name)) BETWEEN 2 AND 100");
                    table.ForeignKey(
                        name: "fk_membership_categories_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_membership_categories_tenant_name",
                table: "membership_categories",
                columns: new[] { "tenant_id", "normalized_name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_members_category_tenant",
                table: "members",
                columns: new[] { "tenant_id", "category_id" },
                principalTable: "membership_categories",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_members_category_tenant",
                table: "members");

            migrationBuilder.DropTable(
                name: "membership_categories");

            migrationBuilder.DropColumn(
                name: "category_id",
                table: "members");

            migrationBuilder.DropColumn(
                name: "security_version",
                table: "admin_users");
        }
    }
}
