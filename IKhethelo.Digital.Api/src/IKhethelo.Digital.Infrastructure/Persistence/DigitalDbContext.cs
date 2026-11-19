using IKhethelo.Digital.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IKhethelo.Digital.Infrastructure.Persistence;

public sealed class DigitalDbContext(DbContextOptions<DigitalDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<MembershipApplication> Applications => Set<MembershipApplication>();
    public DbSet<Member> Members => Set<Member>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<MembershipCategory> MembershipCategories => Set<MembershipCategory>();
    public DbSet<NtmVeteran> NtmVeterans => Set<NtmVeteran>();
    public DbSet<NtmBusiness> NtmBusinesses => Set<NtmBusiness>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.Conventions.Remove(typeof(Microsoft.EntityFrameworkCore.Metadata.Conventions.ForeignKeyIndexConvention));

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfiguration(new NtmVeteranConfiguration());
        model.ApplyConfiguration(new NtmBusinessConfiguration());
        model.HasSequence<long>("member_number_seq").StartsAt(1).IncrementsBy(1).HasMin(1);
        var tenant = Table<Tenant>(model, "tenants");
        Text(tenant, "Code", "code", 50); Text(tenant, "Name", "name", 200);
        Active(tenant); Timestamp(tenant, "CreatedAtUtc", "created_at_utc"); Timestamp(tenant, "UpdatedAtUtc", "updated_at_utc");
        tenant.HasAlternateKey(x => x.Code).HasName("uq_tenants_code");
        Check(tenant, "tenants", "ck_tenants_code_not_blank", "btrim(code) <> ''");
        Check(tenant, "tenants", "ck_tenants_name_not_blank", "btrim(name) <> ''");

        var admin = Table<AdminUser>(model, "admin_users");
        admin.Property(x => x.SecurityVersion).HasColumnName("security_version").HasDefaultValue(0);
        admin.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        Text(admin, "Email", "email", 320); Text(admin, "NormalizedEmail", "normalized_email", 320);
        admin.Property(x => x.PasswordHash).HasColumnName("password_hash").HasColumnType("text").IsRequired();
        admin.Property(x => x.Role).HasColumnName("role").HasColumnType("character varying(50)").HasMaxLength(50).HasConversion<string>();
        Active(admin); Timestamp(admin, "CreatedAtUtc", "created_at_utc"); Timestamp(admin, "UpdatedAtUtc", "updated_at_utc");
        admin.Property(x => x.LastLoginAtUtc).HasColumnName("last_login_at_utc").HasColumnType("timestamp with time zone");
        admin.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_admin_users_tenant");
        admin.HasAlternateKey(x => x.NormalizedEmail).HasName("uq_admin_users_normalized_email");
        admin.HasIndex(x => x.TenantId).HasDatabaseName("ix_admin_users_tenant_id");
        Check(admin, "admin_users", "ck_admin_users_role", "role IN ('TenantAdmin', 'IKhetheloAdmin')");
        Check(admin, "admin_users", "ck_admin_users_tenant_scope", "(role = 'TenantAdmin' AND tenant_id IS NOT NULL) OR (role = 'IKhetheloAdmin' AND tenant_id IS NULL)");

        var application = Table<MembershipApplication>(model, "membership_applications");
        Person(application); Timestamp(application, "SubmittedAtUtc", "submitted_at_utc"); Timestamp(application, "UpdatedAtUtc", "updated_at_utc");
        application.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        application.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_membership_applications_tenant");
        application.Property(x => x.Status).HasColumnName("status").HasColumnType("character varying(30)").HasMaxLength(30).HasConversion(v => v == MembershipApplicationStatus.AwaitingInPersonCompletion ? "Pending" : v == MembershipApplicationStatus.Completed ? "Approved" : "Rejected", v => v == "Pending" ? MembershipApplicationStatus.AwaitingInPersonCompletion : v == "Approved" ? MembershipApplicationStatus.Completed : MembershipApplicationStatus.LegacyClosed).HasDefaultValue(MembershipApplicationStatus.AwaitingInPersonCompletion).ValueGeneratedNever();
        application.Property(x => x.TermsAccepted).HasColumnName("terms_accepted").HasColumnType("boolean");
        application.Property(x => x.PrivacyConsent).HasColumnName("privacy_consent").HasColumnType("boolean");
        application.Property(x => x.ReviewedAtUtc).HasColumnName("reviewed_at_utc").HasColumnType("timestamp with time zone");
        application.Property(x => x.ReviewedByAdminId).HasColumnName("reviewed_by_admin_id").HasColumnType("uuid");
        application.HasOne(x => x.ReviewedByAdmin).WithMany().HasForeignKey(x => x.ReviewedByAdminId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_membership_applications_reviewed_by");
        application.Property(x => x.ReviewNotes).HasColumnName("review_notes").HasColumnType("character varying(2000)").HasMaxLength(2000);
        Check(application, "membership_applications", "ck_membership_applications_status", "status IN ('Pending', 'Approved', 'Rejected')");
        Check(application, "membership_applications", "ck_membership_applications_terms", "terms_accepted = true");
        Check(application, "membership_applications", "ck_membership_applications_privacy", "privacy_consent = true");
        Check(application, "membership_applications", "ck_membership_applications_dob", "date_of_birth <= CURRENT_DATE");
        Check(application, "membership_applications", "ck_membership_applications_names", "char_length(btrim(first_name)) >= 2 AND char_length(btrim(last_name)) >= 2");
        Check(application, "membership_applications", "ck_membership_applications_review_state", "(status = 'Pending' AND reviewed_at_utc IS NULL AND reviewed_by_admin_id IS NULL) OR (status IN ('Approved', 'Rejected') AND reviewed_at_utc IS NOT NULL AND reviewed_by_admin_id IS NOT NULL)");
        application.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_membership_applications_tenant_status");
        application.HasIndex(x => new { x.TenantId, x.SubmittedAtUtc }).IsDescending(false, true).HasDatabaseName("ix_membership_applications_tenant_submitted");
        application.HasIndex(x => new { x.TenantId, x.NormalizedEmail }, "ix_membership_applications_tenant_email");
        application.HasIndex(x => new { x.TenantId, x.NormalizedPhoneNumber }, "ix_membership_applications_tenant_phone");
        application.HasIndex(x => new { x.TenantId, x.NormalizedEmail }, "ux_membership_applications_pending_email").IsUnique().HasFilter("status = 'Pending'");
        application.HasIndex(x => new { x.TenantId, x.NormalizedPhoneNumber }, "ux_membership_applications_pending_phone").IsUnique().HasFilter("status = 'Pending'");

        var member = Table<Member>(model, "members");
        member.Property(x => x.CategoryId).HasColumnName("category_id");
        member.HasOne(x => x.Category).WithMany().HasForeignKey(x => new { x.TenantId, x.CategoryId })
            .HasPrincipalKey(x => new { x.TenantId, x.Id }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_members_category_tenant");
        Person(member); Timestamp(member, "JoinedAtUtc", "joined_at_utc"); Timestamp(member, "UpdatedAtUtc", "updated_at_utc");
        member.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        member.Property(x => x.ApplicationId).HasColumnName("application_id").HasColumnType("uuid");
        Text(member, "MemberNumber", "member_number", 50);
        member.Property(x => x.Status).HasColumnName("status").HasColumnType("character varying(30)").HasMaxLength(30).HasConversion<string>().HasDefaultValue(MemberStatus.Active).ValueGeneratedNever();
        member.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_members_tenant");
        member.HasOne(x => x.Application).WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_members_application");
        member.HasAlternateKey(x => x.ApplicationId).HasName("uq_members_application");
        member.HasAlternateKey(x => new { x.TenantId, x.MemberNumber }).HasName("uq_members_tenant_member_number");
        Check(member, "members", "ck_members_status", "status IN ('Active', 'Suspended', 'Inactive')");
        Check(member, "members", "ck_members_names", "char_length(btrim(first_name)) >= 2 AND char_length(btrim(last_name)) >= 2");
        member.HasIndex(x => new { x.TenantId, x.Status }).HasDatabaseName("ix_members_tenant_status");
        member.HasIndex(x => new { x.TenantId, x.JoinedAtUtc }).IsDescending(false, true).HasDatabaseName("ix_members_tenant_joined");
        member.HasIndex(x => new { x.TenantId, x.NormalizedEmail }, "ix_members_tenant_email");
        member.HasIndex(x => new { x.TenantId, x.NormalizedPhoneNumber }, "ix_members_tenant_phone");
        member.HasIndex(x => new { x.TenantId, x.MemberNumber }).HasDatabaseName("ix_members_tenant_member_number");
        member.HasIndex(x => new { x.TenantId, x.NormalizedEmail }, "ux_members_active_email").IsUnique().HasFilter("status = 'Active'");
        member.HasIndex(x => new { x.TenantId, x.NormalizedPhoneNumber }, "ux_members_active_phone").IsUnique().HasFilter("status = 'Active'");

        var audit = Table<AuditLog>(model, "audit_logs");
        audit.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
        audit.Property(x => x.ActorAdminUserId).HasColumnName("actor_admin_user_id").HasColumnType("uuid");
        audit.Property(x => x.EntityId).HasColumnName("entity_id").HasColumnType("uuid");
        Text(audit, "Action", "action", 150); Text(audit, "EntityType", "entity_type", 150);
        audit.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasColumnType("character varying(150)").HasMaxLength(150);
        audit.Property(x => x.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        Timestamp(audit, "CreatedAtUtc", "created_at_utc");
        audit.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_audit_logs_tenant");
        audit.HasOne(x => x.ActorAdminUser).WithMany().HasForeignKey(x => x.ActorAdminUserId).OnDelete(DeleteBehavior.SetNull).HasConstraintName("fk_audit_logs_actor");
        Check(audit, "audit_logs", "ck_audit_logs_action_not_blank", "btrim(action) <> ''");
        Check(audit, "audit_logs", "ck_audit_logs_entity_type_not_blank", "btrim(entity_type) <> ''");
        audit.HasIndex(x => new { x.TenantId, x.CreatedAtUtc }).IsDescending(false, true).HasDatabaseName("ix_audit_logs_tenant_created");
        audit.HasIndex(x => new { x.ActorAdminUserId, x.CreatedAtUtc }).IsDescending(false, true).HasDatabaseName("ix_audit_logs_actor_created");
        audit.HasIndex(x => new { x.EntityType, x.EntityId }).HasDatabaseName("ix_audit_logs_entity");

        var category = Table<MembershipCategory>(model, "membership_categories");
        category.Property(x => x.TenantId).HasColumnName("tenant_id");
        Text(category, "Name", "name", 100); Text(category, "NormalizedName", "normalized_name", 100);
        Active(category); Timestamp(category, "CreatedAtUtc", "created_at_utc"); Timestamp(category, "UpdatedAtUtc", "updated_at_utc");
        category.HasAlternateKey(x => new { x.TenantId, x.Id }).HasName("uq_membership_categories_tenant_id");
        category.HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique().HasDatabaseName("ux_membership_categories_tenant_name");
        category.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("fk_membership_categories_tenant");
        Check(category, "membership_categories", "ck_membership_categories_name", "char_length(btrim(name)) BETWEEN 2 AND 100");

        // All approved indexes are explicit above; implicit FK indexes are disabled in ConfigureConventions.
    }

    private static EntityTypeBuilder<T> Table<T>(ModelBuilder model, string name) where T : class
    {
        var builder = model.Entity<T>(); builder.ToTable(name);
        builder.HasKey("Id").HasName(name + "_pkey");
        builder.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid").ValueGeneratedNever();
        return builder;
    }
    private static void Text<T>(EntityTypeBuilder<T> b, string property, string column, int length) where T : class =>
        b.Property<string>(property).HasColumnName(column).HasColumnType($"character varying({length})").HasMaxLength(length).IsRequired();
    private static void Timestamp<T>(EntityTypeBuilder<T> b, string property, string column) where T : class =>
        b.Property<DateTimeOffset>(property).HasColumnName(column).HasColumnType("timestamp with time zone").HasDefaultValueSql("CURRENT_TIMESTAMP").ValueGeneratedNever();
    private static void Active<T>(EntityTypeBuilder<T> b) where T : class =>
        b.Property<bool>("IsActive").HasColumnName("is_active").HasColumnType("boolean").HasDefaultValue(true).ValueGeneratedNever();
    private static void Person<T>(EntityTypeBuilder<T> b) where T : class
    {
        Text(b, "FirstName", "first_name", 100); Text(b, "LastName", "last_name", 100);
        b.Property<DateOnly>("DateOfBirth").HasColumnName("date_of_birth").HasColumnType("date");
        Text(b, "PhoneNumber", "phone_number", 50); Text(b, "NormalizedPhoneNumber", "normalized_phone_number", 50);
        Text(b, "Email", "email", 320); Text(b, "NormalizedEmail", "normalized_email", 320);
    }
    private static void Check<T>(EntityTypeBuilder<T> b, string table, string name, string sql) where T : class =>
        b.ToTable(table, t => t.HasCheckConstraint(name, sql));
}

