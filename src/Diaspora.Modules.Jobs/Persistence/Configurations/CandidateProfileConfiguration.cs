using Diaspora.Modules.Jobs.Domain.Candidates;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps the candidate aggregate and its eight owned collections — nine tables.
/// <para>
/// <b>Two indexes here exist for candidate safety rather than for speed.</b> The visibility
/// column is indexed because every candidate search must filter on it, and an unindexed
/// predicate is the kind a future optimisation quietly drops. The user column is unique because
/// one member has exactly one profile, and a duplicate would mean an employer seeing a stale
/// copy of someone's visa status after they had corrected it.
/// </para>
/// <para>
/// The work-authorisation table is keyed on (candidate, country): one answer per jurisdiction,
/// enforced in the database as well as in the aggregate, because a candidate who was both a
/// citizen of somewhere and in need of sponsorship for it would break every filter that reads it.
/// </para>
/// </summary>
public sealed class CandidateProfileConfiguration : IEntityTypeConfiguration<CandidateProfile>
{
    public void Configure(EntityTypeBuilder<CandidateProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.CandidateProfilesTable, JobsModuleMetadata.Schema);

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id)
            .HasConversion(JobValueConverters.Id<CandidateProfileId>());

        builder.Property(profile => profile.UserId)
            .HasConversion(JobValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(profile => profile.CountryCode)
            .HasMaxLength(JobPersistenceConstants.CountryCodeLength);

        builder.Property(profile => profile.CityName)
            .HasMaxLength(JobLimits.MaxLocationName);

        builder.Property(profile => profile.Visibility)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(profile => profile.RemotePreference)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(profile => profile.ExperienceLevel)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(profile => profile.HighestEducation)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(profile => profile.Revision).IsConcurrencyToken();

        ConfigureHeadline(builder);
        ConfigureSalaryExpectation(builder);

        ConfigureSkills(builder);
        ConfigureExperience(builder);
        ConfigureEducation(builder);
        ConfigureCertifications(builder);
        ConfigureLanguages(builder);
        ConfigureWorkAuthorizations(builder);
        ConfigurePreferredLocations(builder);
        ConfigurePortfolio(builder);

        // One profile per member. A duplicate would let an employer see a stale copy of
        // someone's visa status after they had corrected it.
        builder.HasIndex(profile => profile.UserId)
            .IsUnique()
            .HasDatabaseName("ux_job_candidates_user");

        // Every candidate search filters on this. Indexed so the predicate is never the thing
        // a future optimisation quietly drops.
        builder.HasIndex(profile => profile.Visibility)
            .HasDatabaseName("ix_job_candidates_visibility");

        builder.HasIndex(profile => new { profile.CountryCode, profile.IsOpenToWork })
            .HasDatabaseName("ix_job_candidates_country_open");

        builder.Ignore(profile => profile.DomainEvents);
        builder.Ignore(profile => profile.OwnerId);
        builder.Ignore(profile => profile.CompletenessPercent);
    }

    private static void ConfigureHeadline(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.OwnsOne(profile => profile.Headline, headline =>
        {
            headline.Property(value => value.Headline)
                .HasColumnName("headline")
                .HasMaxLength(JobLimits.MaxHeadline)
                .IsRequired();

            headline.Property(value => value.Summary)
                .HasColumnName("summary")
                .HasMaxLength(JobLimits.MaxCandidateSummary);
        });

        builder.Navigation(profile => profile.Headline).IsRequired();
    }

    private static void ConfigureSalaryExpectation(EntityTypeBuilder<CandidateProfile> builder)
    {
        builder.OwnsOne(profile => profile.SalaryExpectation, expectation =>
        {
            expectation.Property(value => value.Minimum)
                .HasColumnName("expected_salary_minimum")
                .HasColumnType(JobPersistenceConstants.MoneyColumnType);

            expectation.Property(value => value.CurrencyCode)
                .HasColumnName("expected_salary_currency")
                .HasMaxLength(JobPersistenceConstants.CurrencyCodeLength);

            expectation.Property(value => value.Period)
                .HasColumnName("expected_salary_period")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength);

            expectation.Property(value => value.IsFlexible)
                .HasColumnName("expected_salary_flexible")
                .IsRequired();

            // Deliberately unindexed. A candidate's expectation is never disclosed to an
            // employer and never searchable — indexing it would be building the index for a
            // query this module refuses to answer.
        });

        builder.Navigation(profile => profile.SalaryExpectation).IsRequired();
    }

    private static void ConfigureSkills(EntityTypeBuilder<CandidateProfile> builder) =>
        builder.OwnsMany(profile => profile.Skills, skill =>
        {
            skill.ToTable(
                JobPersistenceConstants.CandidateSkillsTable, JobsModuleMetadata.Schema);

            skill.WithOwner().HasForeignKey("candidate_id");
            skill.HasKey("candidate_id", nameof(CandidateSkill.SkillKey));

            skill.Property(value => value.SkillKey)
                .HasMaxLength(JobLimits.MaxSkillKey)
                .IsRequired();

            skill.Property(value => value.DisplayName)
                .HasMaxLength(JobLimits.MaxDisplayName)
                .IsRequired();

            skill.Property(value => value.Proficiency)
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            skill.Property(value => value.YearsOfExperience);
            skill.Property(value => value.Order).IsRequired();

            // Keyed identically to the posting side, so a skill search joins cleanly.
            skill.HasIndex(value => value.SkillKey)
                .HasDatabaseName("ix_job_candidate_skills_key");
        });

    private static void ConfigureExperience(EntityTypeBuilder<CandidateProfile> builder) =>
        builder.OwnsMany(profile => profile.Experience, entry =>
        {
            entry.ToTable(
                JobPersistenceConstants.CandidateExperienceTable, JobsModuleMetadata.Schema);

            entry.HasKey(value => value.EntryId);
            entry.Property(value => value.EntryId).ValueGeneratedNever();

            entry.Property(value => value.Title)
                .HasMaxLength(JobLimits.MaxDisplayName)
                .IsRequired();

            entry.Property(value => value.Organization)
                .HasMaxLength(JobLimits.MaxOrganizationName)
                .IsRequired();

            entry.Property(value => value.CountryCode)
                .HasMaxLength(JobPersistenceConstants.CountryCodeLength);

            entry.Property(value => value.CityName).HasMaxLength(JobLimits.MaxLocationName);

            entry.Property(value => value.Description)
                .HasMaxLength(JobLimits.MaxExperienceDescription);

            entry.Property(value => value.Order).IsRequired();

            ConfigurePeriod(entry);

            entry.Ignore(value => value.IsCurrent);
        });

    private static void ConfigureEducation(EntityTypeBuilder<CandidateProfile> builder) =>
        builder.OwnsMany(profile => profile.Education, entry =>
        {
            entry.ToTable(
                JobPersistenceConstants.CandidateEducationTable, JobsModuleMetadata.Schema);

            entry.HasKey(value => value.EntryId);
            entry.Property(value => value.EntryId).ValueGeneratedNever();

            entry.Property(value => value.Institution)
                .HasMaxLength(JobLimits.MaxOrganizationName)
                .IsRequired();

            entry.Property(value => value.Qualification).HasMaxLength(JobLimits.MaxDisplayName);
            entry.Property(value => value.FieldOfStudy).HasMaxLength(JobLimits.MaxDisplayName);

            entry.Property(value => value.Level)
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            // Where the award was made decides whether it is recognised. Stored beside the
            // qualification for exactly that reason.
            entry.Property(value => value.CountryCode)
                .HasMaxLength(JobPersistenceConstants.CountryCodeLength);

            entry.Property(value => value.Order).IsRequired();

            ConfigurePeriod(entry);
        });

    private static void ConfigureCertifications(EntityTypeBuilder<CandidateProfile> builder) =>
        builder.OwnsMany(profile => profile.Certifications, entry =>
        {
            entry.ToTable(
                JobPersistenceConstants.CandidateCertificationsTable, JobsModuleMetadata.Schema);

            entry.HasKey(value => value.EntryId);
            entry.Property(value => value.EntryId).ValueGeneratedNever();

            entry.Property(value => value.Name)
                .HasMaxLength(JobLimits.MaxDisplayName)
                .IsRequired();

            entry.Property(value => value.Issuer).HasMaxLength(JobLimits.MaxOrganizationName);

            entry.Property(value => value.CredentialId)
                .HasMaxLength(JobLimits.MaxRegistrationNumber);

            entry.Property(value => value.CountryCode)
                .HasMaxLength(JobPersistenceConstants.CountryCodeLength);

            entry.Property(value => value.IssuedOn);
            entry.Property(value => value.ExpiresOn);
            entry.Property(value => value.MediaId);
            entry.Property(value => value.Order).IsRequired();
        });

    private static void ConfigureLanguages(EntityTypeBuilder<CandidateProfile> builder) =>
        builder.OwnsMany(profile => profile.Languages, language =>
        {
            language.ToTable(
                JobPersistenceConstants.CandidateLanguagesTable, JobsModuleMetadata.Schema);

            language.WithOwner().HasForeignKey("candidate_id");
            language.HasKey("candidate_id", nameof(CandidateLanguage.Code));

            language.Property(value => value.Code)
                .HasMaxLength(JobPersistenceConstants.LanguageCodeLength)
                .IsRequired();

            language.Property(value => value.Proficiency)
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            language.Property(value => value.Order).IsRequired();

            language.HasIndex(value => value.Code)
                .HasDatabaseName("ix_job_candidate_languages_code");
        });

    private static void ConfigureWorkAuthorizations(EntityTypeBuilder<CandidateProfile> builder) =>
        builder.OwnsMany(profile => profile.WorkAuthorizations, authorization =>
        {
            authorization.ToTable(
                JobPersistenceConstants.CandidateWorkAuthorizationsTable,
                JobsModuleMetadata.Schema);

            // One answer per country, enforced by the key. A candidate who was both a citizen
            // of somewhere and in need of sponsorship for it would break every filter.
            authorization.WithOwner().HasForeignKey("candidate_id");
            authorization.HasKey(
                "candidate_id", nameof(CandidateWorkAuthorization.CountryCode));

            authorization.Property(value => value.CountryCode)
                .HasMaxLength(JobPersistenceConstants.CountryCodeLength)
                .IsRequired();

            authorization.Property(value => value.Kind)
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength)
                .IsRequired();

            authorization.Property(value => value.ExpiresOn);
            authorization.Property(value => value.Order).IsRequired();

            authorization.HasIndex(value => new { value.CountryCode, value.Kind })
                .HasDatabaseName("ix_job_candidate_work_auth_country_kind");
        });

    private static void ConfigurePreferredLocations(EntityTypeBuilder<CandidateProfile> builder) =>
        builder.OwnsMany(profile => profile.PreferredLocations, location =>
        {
            location.ToTable(
                JobPersistenceConstants.CandidatePreferredLocationsTable,
                JobsModuleMetadata.Schema);

            location.WithOwner().HasForeignKey("candidate_id");
            location.HasKey(
                "candidate_id",
                nameof(CandidatePreferredLocation.CountryCode),
                nameof(CandidatePreferredLocation.Order));

            location.Property(value => value.CountryCode)
                .HasMaxLength(JobPersistenceConstants.CountryCodeLength)
                .IsRequired();

            location.Property(value => value.CityName).HasMaxLength(JobLimits.MaxLocationName);
            location.Property(value => value.WillRelocate).IsRequired();
            location.Property(value => value.Order).IsRequired();

            location.HasIndex(value => value.CountryCode)
                .HasDatabaseName("ix_job_candidate_preferred_country");
        });

    private static void ConfigurePortfolio(EntityTypeBuilder<CandidateProfile> builder) =>
        builder.OwnsMany(profile => profile.Portfolio, item =>
        {
            item.ToTable(
                JobPersistenceConstants.CandidatePortfolioTable, JobsModuleMetadata.Schema);

            item.HasKey(value => value.ItemId);
            item.Property(value => value.ItemId).ValueGeneratedNever();

            item.Property(value => value.Label)
                .HasMaxLength(JobLimits.MaxDisplayName)
                .IsRequired();

            item.Property(value => value.Url).HasMaxLength(JobLimits.MaxUrl);
            item.Property(value => value.MediaId);
            item.Property(value => value.Description).HasMaxLength(JobLimits.MaxBulletLength);
            item.Property(value => value.Order).IsRequired();
        });

    /// <summary>
    /// The shared employment-period mapping. Declared once because the storage form of a value
    /// object is a property of the value object — two inline versions that differed in one
    /// detail would produce experience and education rows that no longer compare.
    /// </summary>
    private static void ConfigurePeriod<TEntity>(
        OwnedNavigationBuilder<CandidateProfile, TEntity> builder)
        where TEntity : class, IHasEmploymentPeriod
    {
        builder.OwnsOne(entry => entry.Period, period =>
        {
            period.Property(value => value.Start)
                .HasColumnName("period_start")
                .IsRequired();

            period.Property(value => value.End).HasColumnName("period_end");

            period.Property(value => value.IsCurrent)
                .HasColumnName("period_is_current")
                .IsRequired();
        });

        builder.Navigation(entry => entry.Period).IsRequired();
    }
}
