using Diaspora.Modules.Jobs.Domain.Alerts;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps standing job alerts. One table — the criteria are an owned value object, stored as
/// columns rather than as JSON.
/// <para>
/// <b>Columns rather than a serialised blob, deliberately.</b> A blob would be simpler to write
/// and impossible to query, and the sweep's work list is exactly a query over these fields.
/// It also means a schema change is a migration rather than a silent deserialisation failure on
/// rows written by an older version.
/// </para>
/// <para>
/// The index on (is_active, last_swept_at) is the sweep's whole access path: it runs over every
/// candidate on the platform on a schedule, takes bounded batches, and advances each alert's own
/// watermark. Without the index that is a full scan every few minutes, forever.
/// </para>
/// </summary>
public sealed class JobAlertConfiguration : IEntityTypeConfiguration<JobAlert>
{
    public void Configure(EntityTypeBuilder<JobAlert> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.AlertsTable, JobsModuleMetadata.Schema);

        builder.HasKey(alert => alert.Id);

        builder.Property(alert => alert.Id)
            .HasConversion(JobValueConverters.Id<JobAlertId>());

        builder.Property(alert => alert.UserId)
            .HasConversion(JobValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(alert => alert.CandidateId)
            .HasConversion(JobValueConverters.Id<CandidateProfileId>());

        builder.Property(alert => alert.Name)
            .HasMaxLength(JobLimits.MaxAlertName)
            .IsRequired();

        builder.Property(alert => alert.Frequency)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(alert => alert.IsActive).IsRequired();

        // The watermark. The correctness mechanism, not a statistic — a sweep asks for postings
        // published since it, so nothing is missed when a run is late and nothing is sent twice
        // when a run overlaps.
        builder.Property(alert => alert.LastSweptAt).IsRequired();

        builder.Property(alert => alert.LastNotifiedAt);
        builder.Property(alert => alert.TotalMatchCount).IsRequired();

        ConfigureCriteria(builder);

        // The sweep's access path.
        builder.HasIndex(alert => new { alert.IsActive, alert.LastSweptAt })
            .HasDatabaseName("ix_job_alerts_due");

        builder.HasIndex(alert => alert.UserId)
            .HasDatabaseName("ix_job_alerts_user");

        builder.Ignore(alert => alert.DomainEvents);
        builder.Ignore(alert => alert.OwnerId);
    }

    private static void ConfigureCriteria(EntityTypeBuilder<JobAlert> builder)
    {
        builder.OwnsOne(alert => alert.Criteria, criteria =>
        {
            criteria.Property(value => value.Keywords)
                .HasColumnName("criteria_keywords")
                .HasMaxLength(JobPersistenceConstants.AlertListLength);

            criteria.Property(value => value.CountryCodes)
                .HasColumnName("criteria_countries")
                .HasMaxLength(JobPersistenceConstants.AlertListLength);

            criteria.Property(value => value.CategoryKey)
                .HasColumnName("criteria_category")
                .HasMaxLength(JobLimits.MaxCategoryKey);

            criteria.Property(value => value.SkillKeys)
                .HasColumnName("criteria_skills")
                .HasMaxLength(JobPersistenceConstants.AlertListLength);

            criteria.Property(value => value.WorkplaceType)
                .HasColumnName("criteria_workplace_type")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength);

            criteria.Property(value => value.JobType)
                .HasColumnName("criteria_job_type")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength);

            criteria.Property(value => value.MinimumExperience)
                .HasColumnName("criteria_minimum_experience")
                .HasConversion<string>()
                .HasMaxLength(JobPersistenceConstants.EnumLength);

            criteria.Property(value => value.VisaSponsorshipOnly)
                .HasColumnName("criteria_visa_sponsorship_only")
                .IsRequired();

            criteria.Property(value => value.VerifiedEmployersOnly)
                .HasColumnName("criteria_verified_employers_only")
                .IsRequired();

            criteria.Property(value => value.MinimumSalary)
                .HasColumnName("criteria_minimum_salary")
                .HasColumnType(JobPersistenceConstants.MoneyColumnType);

            criteria.Property(value => value.SalaryCurrency)
                .HasColumnName("criteria_salary_currency")
                .HasMaxLength(JobPersistenceConstants.CurrencyCodeLength);
        });

        // Required with mostly-nullable scalars: the navigation is always present, so the sweep
        // never branches on a missing criteria object. The aggregate refuses an empty one, so
        // "present but matching everything" cannot occur.
        builder.Navigation(alert => alert.Criteria).IsRequired();
    }
}
