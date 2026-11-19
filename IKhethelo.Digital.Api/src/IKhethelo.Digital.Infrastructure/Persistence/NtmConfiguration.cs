using IKhethelo.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IKhethelo.Digital.Infrastructure.Persistence;

internal sealed class NtmVeteranConfiguration : IEntityTypeConfiguration<NtmVeteran>
{
    public void Configure(EntityTypeBuilder<NtmVeteran> b)
    {
        NtmMapping.Base(b, "ntm_veterans");
        b.Property(x => x.FullName).HasColumnName("full_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.MkmvaNumber).HasColumnName("mkmva_number").HasMaxLength(100);
        b.Property(x => x.DmvNumber).HasColumnName("dmv_number").HasMaxLength(100);
        b.Property(x => x.ContactNumber).HasColumnName("contact_number").HasMaxLength(50);
        b.Property(x => x.EmailAddress).HasColumnName("email_address").HasMaxLength(320);
        b.Property(x => x.MembershipReference).HasColumnName("membership_reference").HasMaxLength(100);
        b.HasIndex(x => new { x.TenantId, x.FullName }).HasDatabaseName("ix_ntm_veterans_tenant_name");
        // Stored generated normalization also protects writes that bypass the application.
        foreach (var field in new[] { "mkmva", "dmv" })
        {
            b.Property<string>("Normalized" + (field == "mkmva" ? "Mkmva" : "Dmv") + "Number")
                .HasColumnName("normalized_" + field + "_number").HasMaxLength(100)
                .HasComputedColumnSql($"nullif(upper(btrim({field}_number)), '')", stored: true);
            b.HasIndex("TenantId", "Normalized" + (field == "mkmva" ? "Mkmva" : "Dmv") + "Number")
                .IsUnique().HasDatabaseName("ux_ntm_veterans_tenant_" + field)
                .HasFilter("normalized_" + field + "_number IS NOT NULL");
        }
        b.HasAlternateKey(x => new { x.TenantId, x.Id }).HasName("uq_ntm_veterans_tenant_id");
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_ntm_veterans_tenant");
        b.ToTable("ntm_veterans", t =>
        {
            t.HasCheckConstraint("ck_ntm_veterans_name", "btrim(full_name) <> ''");
            t.HasCheckConstraint("ck_ntm_veterans_registration", "nullif(btrim(mkmva_number), '') IS NOT NULL OR nullif(btrim(dmv_number), '') IS NOT NULL");
        });
    }
}

internal sealed class NtmBusinessConfiguration : IEntityTypeConfiguration<NtmBusiness>
{
    public void Configure(EntityTypeBuilder<NtmBusiness> b)
    {
        NtmMapping.Base(b, "ntm_businesses");
        b.Property(x => x.VeteranId).HasColumnName("veteran_id");
        b.Property(x => x.BusinessName).HasColumnName("business_name").HasMaxLength(200).IsRequired();
        b.Property(x => x.CipcRegistrationNumber).HasColumnName("cipc_registration_number").HasMaxLength(100);
        b.Property(x => x.BusinessAddress).HasColumnName("business_address").HasColumnType("text");
        b.Property(x => x.Sector).HasColumnName("sector").HasMaxLength(200);
        b.Property(x => x.BusinessBackground).HasColumnName("business_background").HasColumnType("text");
        b.Property(x => x.OperationalStatus).HasColumnName("operational_status").HasMaxLength(30);
        b.Property(x => x.NumberOfEmployees).HasColumnName("number_of_employees");
        b.Property(x => x.YearsInOperation).HasColumnName("years_in_operation").HasPrecision(6, 1);
        b.Property(x => x.CurrentBusinessNeeds).HasColumnName("current_business_needs").HasColumnType("text");
        b.Property(x => x.PriorityNeed).HasColumnName("priority_need").HasColumnType("text");
        b.Property(x => x.FundingRequired).HasColumnName("funding_required");
        b.Property(x => x.FundingAmount).HasColumnName("funding_amount").HasPrecision(15, 2);
        b.Property(x => x.AdditionalComments).HasColumnName("additional_comments").HasColumnType("text");
        b.Property(x => x.AssessmentDate).HasColumnName("assessment_date").HasColumnType("date");
        b.Property(x => x.AssessedBy).HasColumnName("assessed_by").HasMaxLength(200);
        b.Property(x => x.SourceRecordNumber).HasColumnName("source_record_number");
        b.Property(x => x.OperationalStatusSource).HasColumnName("operational_status_source").HasColumnType("text");
        b.Property(x => x.NumberOfEmployeesSource).HasColumnName("number_of_employees_source").HasColumnType("text");
        b.Property(x => x.YearsInOperationSource).HasColumnName("years_in_operation_source").HasColumnType("text");
        b.Property(x => x.FundingRequiredSource).HasColumnName("funding_required_source").HasColumnType("text");
        b.HasIndex(x => new { x.TenantId, x.BusinessName }).HasDatabaseName("ix_ntm_businesses_tenant_name");
        b.HasIndex(x => new { x.TenantId, x.AssessmentDate }).HasDatabaseName("ix_ntm_businesses_tenant_assessment_date");
        b.HasIndex(x => new { x.TenantId, x.VeteranId }).IsUnique().HasDatabaseName("ux_ntm_businesses_tenant_veteran");
        b.HasIndex(x => new { x.TenantId, x.Sector }).HasDatabaseName("ix_ntm_businesses_tenant_sector");
        b.HasIndex(x => new { x.TenantId, x.OperationalStatus }).HasDatabaseName("ix_ntm_businesses_tenant_status");
        b.HasOne(x => x.Veteran).WithOne(x => x.Business).HasForeignKey<NtmBusiness>(x => new { x.TenantId, x.VeteranId })
            .HasPrincipalKey<NtmVeteran>(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_ntm_businesses_veteran_tenant");
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_ntm_businesses_tenant");
        b.ToTable("ntm_businesses", t =>
        {
            t.HasCheckConstraint("ck_ntm_businesses_name", "btrim(business_name) <> ''");
            t.HasCheckConstraint("ck_ntm_businesses_status", "operational_status IN ('Operational', 'Partially Operational', 'Not Operational')");
            t.HasCheckConstraint("ck_ntm_businesses_employees", "number_of_employees >= 0");
            t.HasCheckConstraint("ck_ntm_businesses_years", "years_in_operation >= 0");
            t.HasCheckConstraint("ck_ntm_businesses_funding", "funding_amount >= 0");
            t.HasCheckConstraint("ck_ntm_businesses_source_record", "source_record_number > 0");
        });
    }
}

internal static class NtmMapping
{
    public static void Base<T>(EntityTypeBuilder<T> b, string table) where T : class
    {
        b.ToTable(table); b.HasKey("Id").HasName(table + "_pkey");
        b.Property<Guid>("Id").HasColumnName("id").ValueGeneratedNever();
        b.Property<Guid>("TenantId").HasColumnName("tenant_id");
        b.Property<DateTimeOffset>("CreatedAtUtc").HasColumnName("created_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedNever();
        b.Property<DateTimeOffset>("UpdatedAtUtc").HasColumnName("updated_at_utc").HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedNever();
    }
}
