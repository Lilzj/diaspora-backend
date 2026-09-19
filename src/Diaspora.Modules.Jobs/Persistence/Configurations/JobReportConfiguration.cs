using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.Reports;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Jobs.Persistence.Configurations;

/// <summary>
/// Maps reports about job adverts. One table.
/// <para>
/// <b>No foreign key to the posting.</b> A report outlives what it was about: an advert can be
/// archived or removed and the record of what was alleged, and what a moderator decided, has to
/// survive it — that trail is what an appeal and an audit are answered from.
/// </para>
/// <para>
/// The unique index on (posting, reporter) is the one member, one report rule in the database
/// rather than only in a handler, because the flag threshold counts people who think something
/// is wrong and a member who could report five times would decide that alone.
/// </para>
/// </summary>
public sealed class JobReportConfiguration : IEntityTypeConfiguration<JobReport>
{
    public void Configure(EntityTypeBuilder<JobReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(JobPersistenceConstants.JobReportsTable, JobsModuleMetadata.Schema);

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id)
            .HasConversion(JobValueConverters.Id<JobReportId>());

        builder.Property(report => report.PostingId)
            .HasConversion(JobValueConverters.Id<JobPostingId>())
            .IsRequired();

        builder.Property(report => report.EmployerId)
            .HasConversion(JobValueConverters.Id<EmployerProfileId>())
            .IsRequired();

        builder.Property(report => report.ReportedBy)
            .HasConversion(JobValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(report => report.Reason)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(report => report.Status)
            .HasConversion<string>()
            .HasMaxLength(JobPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(report => report.Detail).HasMaxLength(JobLimits.MaxReportDetail);

        builder.Property(report => report.Resolution).HasMaxLength(JobLimits.MaxModerationNote);

        // The same converter as the required identifiers: it carries null through, and the
        // audit columns are configured centrally for every IAuditableEntity.
        builder.Property(report => report.ResolvedBy)
            .HasConversion(JobValueConverters.Id<UserId>());

        // One member, one report per advert.
        builder.HasIndex(report => new { report.PostingId, report.ReportedBy })
            .IsUnique()
            .HasDatabaseName("ux_job_reports_posting_reporter");

        // The queue: what is open, oldest first.
        builder.HasIndex(report => new { report.Status, report.CreatedAt })
            .HasDatabaseName("ix_job_reports_status_created");

        // "What else has this employer had upheld" — the pattern that matters in recruitment fraud.
        builder.HasIndex(report => new { report.EmployerId, report.Status })
            .HasDatabaseName("ix_job_reports_employer_status");

        builder.Ignore(report => report.DomainEvents);
        builder.Ignore(report => report.IsOpen);
        builder.Ignore(report => report.FlagsImmediately);
    }
}
