using Diaspora.Modules.Jobs.Domain.Analytics;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps the per-posting analytics rollup. One table, all scalars.
/// <para>
/// <b>Every column is a counter or a running total, and the table has no child rows at all.</b>
/// That is what makes the rollup a fixed size no matter how successful a posting is — a job with
/// four hundred thousand impressions has exactly the same footprint as one with four. The trade
/// is that the funnel cannot be re-sliced by date after the fact, which is a reporting feature
/// and belongs in an analytics store rather than in the write model.
/// </para>
/// </summary>
public sealed class JobAnalyticsConfiguration : IEntityTypeConfiguration<JobAnalytics>
{
    public void Configure(EntityTypeBuilder<JobAnalytics> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.AnalyticsTable, JobsModuleMetadata.Schema);

        builder.HasKey(analytics => analytics.Id);

        builder.Property(analytics => analytics.Id)
            .HasConversion(JobValueConverters.Id<JobAnalyticsId>());

        builder.Property(analytics => analytics.PostingId)
            .HasConversion(JobValueConverters.Id<JobPostingId>())
            .IsRequired();

        builder.Property(analytics => analytics.EmployerId)
            .HasConversion(JobValueConverters.Id<EmployerProfileId>())
            .IsRequired();

        // The applicant country tally, as a compact delimited string. Read as a whole or not at
        // all, on one screen — a row per country per posting would be a join and an index to
        // answer a question that fits in a few hundred bytes.
        builder.Property(analytics => analytics.ApplicantCountryTally)
            .HasMaxLength(JobPersistenceConstants.CountryTallyLength);

        // One rollup per posting.
        builder.HasIndex(analytics => analytics.PostingId)
            .IsUnique()
            .HasDatabaseName("ux_job_analytics_posting");

        // The employer-wide funnel.
        builder.HasIndex(analytics => analytics.EmployerId)
            .HasDatabaseName("ix_job_analytics_employer");

        builder.Ignore(analytics => analytics.DomainEvents);
        builder.Ignore(analytics => analytics.ClickThroughRate);
        builder.Ignore(analytics => analytics.ApplicationRate);
        builder.Ignore(analytics => analytics.ApplicationCompletionRate);
        builder.Ignore(analytics => analytics.ConversionRate);
        builder.Ignore(analytics => analytics.OfferAcceptanceRate);
        builder.Ignore(analytics => analytics.AverageResponseHours);
        builder.Ignore(analytics => analytics.AverageTimeToHireDays);
        builder.Ignore(analytics => analytics.CountryTally);
    }
}
