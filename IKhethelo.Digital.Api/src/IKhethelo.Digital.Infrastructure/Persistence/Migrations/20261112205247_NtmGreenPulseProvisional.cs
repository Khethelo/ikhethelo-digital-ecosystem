using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IKhethelo.Digital.Infrastructure.Persistence.Migrations
{
    // PROVISIONAL: do not apply until docs/NTM_SPREADSHEET_FIELD_MAPPING.md is verified.
    public partial class NtmGreenPulseProvisional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ntm_veterans",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mkmva_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    dmv_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    contact_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    normalized_dmv_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, computedColumnSql: "nullif(upper(btrim(dmv_number)), '')", stored: true),
                    normalized_mkmva_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true, computedColumnSql: "nullif(upper(btrim(mkmva_number)), '')", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("ntm_veterans_pkey", x => x.id);
                    table.UniqueConstraint("uq_ntm_veterans_tenant_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_ntm_veterans_name", "btrim(full_name) <> ''");
                    table.CheckConstraint("ck_ntm_veterans_registration", "nullif(btrim(mkmva_number), '') IS NOT NULL OR nullif(btrim(dmv_number), '') IS NOT NULL");
                    table.ForeignKey(
                        name: "fk_ntm_veterans_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ntm_businesses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    veteran_id = table.Column<Guid>(type: "uuid", nullable: false),
                    business_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cipc_registration_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    business_address = table.Column<string>(type: "text", nullable: true),
                    sector = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    business_background = table.Column<string>(type: "text", nullable: true),
                    operational_status = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    number_of_employees = table.Column<int>(type: "integer", nullable: true),
                    years_in_operation = table.Column<decimal>(type: "numeric", nullable: true),
                    current_business_needs = table.Column<string>(type: "text", nullable: true),
                    priority_need = table.Column<string>(type: "text", nullable: true),
                    funding_required = table.Column<bool>(type: "boolean", nullable: true),
                    funding_amount = table.Column<decimal>(type: "numeric", nullable: true),
                    additional_comments = table.Column<string>(type: "text", nullable: true),
                    assessment_date = table.Column<DateOnly>(type: "date", nullable: true),
                    assessed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("ntm_businesses_pkey", x => x.id);
                    table.CheckConstraint("ck_ntm_businesses_employees", "number_of_employees >= 0");
                    table.CheckConstraint("ck_ntm_businesses_funding", "funding_amount >= 0");
                    table.CheckConstraint("ck_ntm_businesses_name", "btrim(business_name) <> ''");
                    table.CheckConstraint("ck_ntm_businesses_status", "operational_status IN ('Operational', 'Partially Operational', 'Not Operational')");
                    table.CheckConstraint("ck_ntm_businesses_years", "years_in_operation >= 0");
                    table.ForeignKey(
                        name: "fk_ntm_businesses_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_ntm_businesses_veteran_tenant",
                        columns: x => new { x.tenant_id, x.veteran_id },
                        principalTable: "ntm_veterans",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ntm_businesses_tenant_sector",
                table: "ntm_businesses",
                columns: new[] { "tenant_id", "sector" });

            migrationBuilder.CreateIndex(
                name: "ix_ntm_businesses_tenant_status",
                table: "ntm_businesses",
                columns: new[] { "tenant_id", "operational_status" });

            migrationBuilder.CreateIndex(
                name: "ux_ntm_businesses_tenant_veteran",
                table: "ntm_businesses",
                columns: new[] { "tenant_id", "veteran_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_ntm_veterans_tenant_dmv",
                table: "ntm_veterans",
                columns: new[] { "tenant_id", "normalized_dmv_number" },
                unique: true,
                filter: "normalized_dmv_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_ntm_veterans_tenant_mkmva",
                table: "ntm_veterans",
                columns: new[] { "tenant_id", "normalized_mkmva_number" },
                unique: true,
                filter: "normalized_mkmva_number IS NOT NULL");

            // Match existing code-based tenant provisioning; preserve any existing identity and activation state.
            migrationBuilder.Sql("""
                INSERT INTO tenants (id, code, name, is_active, created_at_utc, updated_at_utc)
                VALUES (gen_random_uuid(), 'NTM', 'NTM GreenPulse Network', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (code) DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Keep the provisioned tenant/admin/audit records; never remove an existing shared tenant.
            migrationBuilder.DropTable(
                name: "ntm_businesses");

            migrationBuilder.DropTable(
                name: "ntm_veterans");
        }
    }
}
