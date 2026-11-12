using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IKhethelo.Digital.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "member_number_seq",
                minValue: 1L);

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("tenants_pkey", x => x.id);
                    table.UniqueConstraint("uq_tenants_code", x => x.code);
                    table.CheckConstraint("ck_tenants_code_not_blank", "btrim(code) <> ''");
                    table.CheckConstraint("ck_tenants_name_not_blank", "btrim(name) <> ''");
                });

            migrationBuilder.CreateTable(
                name: "admin_users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    last_login_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("admin_users_pkey", x => x.id);
                    table.UniqueConstraint("uq_admin_users_normalized_email", x => x.normalized_email);
                    table.CheckConstraint("ck_admin_users_role", "role IN ('TenantAdmin', 'IKhetheloAdmin')");
                    table.CheckConstraint("ck_admin_users_tenant_scope", "(role = 'TenantAdmin' AND tenant_id IS NOT NULL) OR (role = 'IKhetheloAdmin' AND tenant_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_admin_users_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_admin_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    action = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("audit_logs_pkey", x => x.id);
                    table.CheckConstraint("ck_audit_logs_action_not_blank", "btrim(action) <> ''");
                    table.CheckConstraint("ck_audit_logs_entity_type_not_blank", "btrim(entity_type) <> ''");
                    table.ForeignKey(
                        name: "fk_audit_logs_actor",
                        column: x => x.actor_admin_user_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_audit_logs_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "membership_applications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Pending"),
                    terms_accepted = table.Column<bool>(type: "boolean", nullable: false),
                    privacy_consent = table.Column<bool>(type: "boolean", nullable: false),
                    submitted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    reviewed_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_admin_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("membership_applications_pkey", x => x.id);
                    table.CheckConstraint("ck_membership_applications_dob", "date_of_birth <= CURRENT_DATE");
                    table.CheckConstraint("ck_membership_applications_names", "char_length(btrim(first_name)) >= 2 AND char_length(btrim(last_name)) >= 2");
                    table.CheckConstraint("ck_membership_applications_privacy", "privacy_consent = true");
                    table.CheckConstraint("ck_membership_applications_review_state", "(status = 'Pending' AND reviewed_at_utc IS NULL AND reviewed_by_admin_id IS NULL) OR (status IN ('Approved', 'Rejected') AND reviewed_at_utc IS NOT NULL AND reviewed_by_admin_id IS NOT NULL)");
                    table.CheckConstraint("ck_membership_applications_status", "status IN ('Pending', 'Approved', 'Rejected')");
                    table.CheckConstraint("ck_membership_applications_terms", "terms_accepted = true");
                    table.ForeignKey(
                        name: "fk_membership_applications_reviewed_by",
                        column: x => x.reviewed_by_admin_id,
                        principalTable: "admin_users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_membership_applications_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "members",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    member_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    normalized_phone_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    normalized_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false, defaultValue: "Active"),
                    joined_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("members_pkey", x => x.id);
                    table.UniqueConstraint("uq_members_application", x => x.application_id);
                    table.UniqueConstraint("uq_members_tenant_member_number", x => new { x.tenant_id, x.member_number });
                    table.CheckConstraint("ck_members_names", "char_length(btrim(first_name)) >= 2 AND char_length(btrim(last_name)) >= 2");
                    table.CheckConstraint("ck_members_status", "status IN ('Active', 'Suspended', 'Inactive')");
                    table.ForeignKey(
                        name: "fk_members_application",
                        column: x => x.application_id,
                        principalTable: "membership_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_members_tenant",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_users_tenant_id",
                table: "admin_users",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_actor_created",
                table: "audit_logs",
                columns: new[] { "actor_admin_user_id", "created_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_entity",
                table: "audit_logs",
                columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_logs_tenant_created",
                table: "audit_logs",
                columns: new[] { "tenant_id", "created_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_members_tenant_email",
                table: "members",
                columns: new[] { "tenant_id", "normalized_email" });

            migrationBuilder.CreateIndex(
                name: "ix_members_tenant_joined",
                table: "members",
                columns: new[] { "tenant_id", "joined_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_members_tenant_member_number",
                table: "members",
                columns: new[] { "tenant_id", "member_number" });

            migrationBuilder.CreateIndex(
                name: "ix_members_tenant_phone",
                table: "members",
                columns: new[] { "tenant_id", "normalized_phone_number" });

            migrationBuilder.CreateIndex(
                name: "ix_members_tenant_status",
                table: "members",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_members_active_email",
                table: "members",
                columns: new[] { "tenant_id", "normalized_email" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ux_members_active_phone",
                table: "members",
                columns: new[] { "tenant_id", "normalized_phone_number" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_membership_applications_tenant_email",
                table: "membership_applications",
                columns: new[] { "tenant_id", "normalized_email" });

            migrationBuilder.CreateIndex(
                name: "ix_membership_applications_tenant_phone",
                table: "membership_applications",
                columns: new[] { "tenant_id", "normalized_phone_number" });

            migrationBuilder.CreateIndex(
                name: "ix_membership_applications_tenant_status",
                table: "membership_applications",
                columns: new[] { "tenant_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_membership_applications_tenant_submitted",
                table: "membership_applications",
                columns: new[] { "tenant_id", "submitted_at_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ux_membership_applications_pending_email",
                table: "membership_applications",
                columns: new[] { "tenant_id", "normalized_email" },
                unique: true,
                filter: "status = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "ux_membership_applications_pending_phone",
                table: "membership_applications",
                columns: new[] { "tenant_id", "normalized_phone_number" },
                unique: true,
                filter: "status = 'Pending'");

            // Preserve the approved code-based idempotent seed; do not store changing timestamps in HasData.
            migrationBuilder.Sql("""
                INSERT INTO tenants (id, code, name, is_active, created_at_utc, updated_at_utc)
                VALUES ('e5a11111-5c1a-4e5a-9e5a-111111111111', 'ESHL', 'Es''hlahleni Social Club', true, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
                ON CONFLICT (code) DO UPDATE
                SET name = EXCLUDED.name, is_active = EXCLUDED.is_active, updated_at_utc = CURRENT_TIMESTAMP;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "members");

            migrationBuilder.DropTable(
                name: "membership_applications");

            migrationBuilder.DropTable(
                name: "admin_users");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropSequence(
                name: "member_number_seq");
        }
    }
}
