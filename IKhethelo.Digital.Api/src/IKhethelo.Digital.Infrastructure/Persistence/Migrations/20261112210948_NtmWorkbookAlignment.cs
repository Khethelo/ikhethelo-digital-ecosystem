using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace IKhethelo.Digital.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class NtmWorkbookAlignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Refuse precision loss if an installation ever used the earlier draft schema.
            // Do not silently round, clamp or delete recorded values during ALTER TYPE.
            migrationBuilder.Sql("""
                LOCK TABLE ntm_businesses IN ACCESS EXCLUSIVE MODE;
                DO $$ BEGIN
                    IF EXISTS (SELECT 1 FROM ntm_businesses
                        WHERE years_in_operation > 99999.9 OR years_in_operation <> trunc(years_in_operation, 1)
                           OR funding_amount > 9999999999999.99 OR funding_amount <> trunc(funding_amount, 2)) THEN
                        RAISE EXCEPTION 'NTM numeric values require explicit reconciliation before workbook alignment.'
                            USING ERRCODE = '23514';
                    END IF;
                END $$;
                """);
            migrationBuilder.AddColumn<string>(
                name: "email_address",
                table: "ntm_veterans",
                type: "character varying(320)",
                maxLength: 320,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "membership_reference",
                table: "ntm_veterans",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "years_in_operation",
                table: "ntm_businesses",
                type: "numeric(6,1)",
                precision: 6,
                scale: 1,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "operational_status",
                table: "ntm_businesses",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "funding_amount",
                table: "ntm_businesses",
                type: "numeric(15,2)",
                precision: 15,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "funding_required_source",
                table: "ntm_businesses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "number_of_employees_source",
                table: "ntm_businesses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "operational_status_source",
                table: "ntm_businesses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "source_record_number",
                table: "ntm_businesses",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "years_in_operation_source",
                table: "ntm_businesses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_ntm_veterans_tenant_name",
                table: "ntm_veterans",
                columns: new[] { "tenant_id", "full_name" });

            migrationBuilder.CreateIndex(
                name: "ix_ntm_businesses_tenant_assessment_date",
                table: "ntm_businesses",
                columns: new[] { "tenant_id", "assessment_date" });

            migrationBuilder.CreateIndex(
                name: "ix_ntm_businesses_tenant_name",
                table: "ntm_businesses",
                columns: new[] { "tenant_id", "business_name" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_ntm_businesses_source_record",
                table: "ntm_businesses",
                sql: "source_record_number > 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_ntm_veterans_tenant_name",
                table: "ntm_veterans");

            migrationBuilder.DropIndex(
                name: "ix_ntm_businesses_tenant_assessment_date",
                table: "ntm_businesses");

            migrationBuilder.DropIndex(
                name: "ix_ntm_businesses_tenant_name",
                table: "ntm_businesses");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ntm_businesses_source_record",
                table: "ntm_businesses");

            migrationBuilder.DropColumn(
                name: "email_address",
                table: "ntm_veterans");

            migrationBuilder.DropColumn(
                name: "membership_reference",
                table: "ntm_veterans");

            migrationBuilder.DropColumn(
                name: "funding_required_source",
                table: "ntm_businesses");

            migrationBuilder.DropColumn(
                name: "number_of_employees_source",
                table: "ntm_businesses");

            migrationBuilder.DropColumn(
                name: "operational_status_source",
                table: "ntm_businesses");

            migrationBuilder.DropColumn(
                name: "source_record_number",
                table: "ntm_businesses");

            migrationBuilder.DropColumn(
                name: "years_in_operation_source",
                table: "ntm_businesses");

            migrationBuilder.AlterColumn<decimal>(
                name: "years_in_operation",
                table: "ntm_businesses",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,1)",
                oldPrecision: 6,
                oldScale: 1,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "operational_status",
                table: "ntm_businesses",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "funding_amount",
                table: "ntm_businesses",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(15,2)",
                oldPrecision: 15,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
