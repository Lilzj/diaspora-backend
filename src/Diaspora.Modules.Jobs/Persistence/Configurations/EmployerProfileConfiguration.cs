using Diaspora.Modules.Jobs.Domain.Employers;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps the employer aggregate and its two owned collections — three tables.
/// <para>
/// The members table is the module's authorisation store in practice: every employer-scoped
/// command loads it to answer "may this caller act here". It is keyed on (employer, user), which
/// makes adding a member idempotent in the database as well as in the aggregate, and indexed on
/// the user so "which employers do I belong to" is a seek rather than a scan of every membership
/// on the platform.
/// </para>
/// </summary>
public sealed class EmployerProfileConfiguration : IEntityTypeConfiguration<EmployerProfile>
{
    public void Configure(EntityTypeBuilder<EmployerProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.EmployerProfilesTable, JobsModuleMetadata.Schema);

        builder.HasKey(employer => employer.Id);

        builder.Property(employer => employer.Id)
            .HasConversion(JobValueConverters.Id<EmployerProfileId>());

        builder.Property(employer => employer.OwnerUserId)
            .HasConversion(JobValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(employer => employer.PrimaryCountryCode)
            .HasMaxLength(JobPersistenceConstants.CountryCodeLength)
            .IsRequired();

        builder.Property(employer => employer.Description)
            .HasMaxLength(JobLimits.MaxEmployerDescription);

        builder.Property(employer => employer.Status)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(employer => employer.Revision).IsConcurrencyToken();

        ConfigureIdentity(builder);
        ConfigureSlug(builder);
        ConfigureContact(builder);
        ConfigureBranding(builder);
        ConfigureTrustMark(builder);
        ConfigureResponsiveness(builder);

        ConfigureMembers(builder);
        ConfigureHiringRegions(builder);

        builder.HasIndex(employer => employer.PrimaryCountryCode)
            .HasDatabaseName("ix_job_employers_country");

        builder.HasIndex(employer => employer.Status)
            .HasDatabaseName("ix_job_employers_status");

        builder.Ignore(employer => employer.DomainEvents);
        builder.Ignore(employer => employer.OwnerId);
        builder.Ignore(employer => employer.CanRecruit);
        builder.Ignore(employer => employer.IsVerified);
        builder.Ignore(employer => employer.IsIntermediary);
    }

    private static void ConfigureIdentity(EntityTypeBuilder<EmployerProfile> builder)
    {
        builder.OwnsOne(employer => employer.Identity, identity =>
        {
            identity.Property(value => value.DisplayName)
                .HasColumnName("display_name")
                .HasMaxLength(JobLimits.MaxEmployerName)
                .IsRequired();

            identity.Property(value => value.LegalName)
                .HasColumnName("legal_name")
                .HasMaxLength(JobLimits.MaxEmployerName);

            identity.Property(value => value.RegistrationNumber)
                .HasColumnName("registration_number")
                .HasMaxLength(JobLimits.MaxRegistrationNumber);

            identity.Property(value => value.OrganizationType)
                .HasColumnName("organization_type")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            identity.Property(value => value.Size)
                .HasColumnName("company_size")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            identity.Property(value => value.FoundedYear).HasColumnName("founded_year");

            identity.HasIndex(value => value.DisplayName)
                .HasDatabaseName("ix_job_employers_display_name");

            identity.HasIndex(value => value.OrganizationType)
                .HasDatabaseName("ix_job_employers_organization_type");
        });

        builder.Navigation(employer => employer.Identity).IsRequired();
    }

    private static void ConfigureSlug(EntityTypeBuilder<EmployerProfile> builder)
    {
        builder.OwnsOne(employer => employer.Slug, slug =>
        {
            slug.Property(value => value.Value)
                .HasColumnName("slug")
                .HasMaxLength(JobLimits.MaxSlug)
                .IsRequired();

            slug.HasIndex(value => value.Value)
                .IsUnique()
                .HasDatabaseName("ux_job_employers_slug");
        });

        builder.Navigation(employer => employer.Slug).IsRequired();
    }

    /// <summary>
    /// Required with all-nullable scalars — the shape B6 established for table-sharing
    /// dependents. The navigation is always present so the read path never branches on a
    /// missing object, exactly as the value object documents.
    /// </summary>
    private static void ConfigureContact(EntityTypeBuilder<EmployerProfile> builder)
    {
        builder.OwnsOne(employer => employer.Contact, contact =>
        {
            contact.Property(value => value.Email)
                .HasColumnName("contact_email")
                .HasMaxLength(320);

            contact.Property(value => value.Phone)
                .HasColumnName("contact_phone")
                .HasMaxLength(40);

            contact.Property(value => value.WebsiteUrl)
                .HasColumnName("website_url")
                .HasMaxLength(JobLimits.MaxUrl);

            contact.Property(value => value.CareersUrl)
                .HasColumnName("careers_url")
                .HasMaxLength(JobLimits.MaxUrl);
        });

        builder.Navigation(employer => employer.Contact).IsRequired();
    }

    private static void ConfigureBranding(EntityTypeBuilder<EmployerProfile> builder)
    {
        builder.OwnsOne(employer => employer.Branding, branding =>
        {
            branding.Property(value => value.LogoMediaId).HasColumnName("logo_media_id");
            branding.Property(value => value.CoverMediaId).HasColumnName("cover_media_id");

            branding.Property(value => value.Tagline)
                .HasColumnName("tagline")
                .HasMaxLength(JobLimits.MaxDisplayName);
        });

        builder.Navigation(employer => employer.Branding).IsRequired();
    }

    private static void ConfigureTrustMark(EntityTypeBuilder<EmployerProfile> builder)
    {
        builder.OwnsOne(employer => employer.TrustMark, mark =>
        {
            mark.Property(value => value.State)
                .HasColumnName("verification_state")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            mark.Property(value => value.VerificationId).HasColumnName("verification_id");

            mark.Property(value => value.Badge)
                .HasColumnName("verification_badge")
                .HasMaxLength(JobPersistenceConstants.BadgeLength);

            mark.Property(value => value.Reason)
                .HasColumnName("verification_reason")
                .HasMaxLength(JobPersistenceConstants.ReasonLength);

            mark.Property(value => value.VerifiedAt).HasColumnName("verified_at");
            mark.Property(value => value.ChangedAt).HasColumnName("verification_changed_at");

            // "Verified employers only" is the filter candidates reach for most on this
            // platform, and the column is owned — so the index lives here.
            mark.HasIndex(value => value.State)
                .HasDatabaseName("ix_job_employers_verification_state");

            // How a Trust outcome finds the employer that raised the claim.
            mark.HasIndex(value => value.VerificationId)
                .HasDatabaseName("ix_job_employers_verification_id");
        });

        builder.Navigation(employer => employer.TrustMark).IsRequired();
    }

    private static void ConfigureResponsiveness(EntityTypeBuilder<EmployerProfile> builder)
    {
        builder.OwnsOne(employer => employer.Responsiveness, metrics =>
        {
            metrics.Property(value => value.RespondedCount).HasColumnName("responded_count");

            metrics.Property(value => value.TotalResponseMinutes)
                .HasColumnName("total_response_minutes");

            metrics.Property(value => value.DecidedCount).HasColumnName("decided_count");
            metrics.Property(value => value.IgnoredCount).HasColumnName("ignored_count");
            metrics.Property(value => value.OffersExtended).HasColumnName("offers_extended");
            metrics.Property(value => value.OffersAccepted).HasColumnName("offers_accepted");
        });

        builder.Navigation(employer => employer.Responsiveness).IsRequired();
    }

    private static void ConfigureMembers(EntityTypeBuilder<EmployerProfile> builder) =>
        builder.OwnsMany(employer => employer.Members, member =>
        {
            member.ToTable(
                JobPersistenceConstants.EmployerMembersTable, JobsModuleMetadata.Schema);

            // (employer, user) is the identity, which is what makes adding idempotent in the
            // database as well as in the aggregate.
            member.WithOwner().HasForeignKey("employer_id");
            member.HasKey("employer_id", nameof(EmployerMember.UserId));

            member.Property(value => value.UserId)
                .HasConversion(JobValueConverters.Id<UserId>())
                .IsRequired();

            member.Property(value => value.Role)
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            member.Property(value => value.JoinedAt).IsRequired();

            // "Which employers do I belong to" is asked on every recruiter request.
            member.HasIndex(value => value.UserId)
                .HasDatabaseName("ix_job_employer_members_user");
        });

    private static void ConfigureHiringRegions(EntityTypeBuilder<EmployerProfile> builder) =>
        builder.OwnsMany(employer => employer.HiringRegions, region =>
        {
            region.ToTable(
                JobPersistenceConstants.EmployerHiringRegionsTable, JobsModuleMetadata.Schema);

            region.WithOwner().HasForeignKey("employer_id");
            region.HasKey("employer_id", nameof(EmployerHiringRegion.CountryCode));

            region.Property(value => value.CountryCode)
                .HasMaxLength(JobPersistenceConstants.CountryCodeLength)
                .IsRequired();

            region.Property(value => value.SponsorsVisas).IsRequired();
            region.Property(value => value.Order).IsRequired();

            // "Which employers hire into my country" — the browse that makes the declared
            // regions worth storing separately from where the postings happen to be.
            region.HasIndex(value => value.CountryCode)
                .HasDatabaseName("ix_job_employer_regions_country");
        });
}
