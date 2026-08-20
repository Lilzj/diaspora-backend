using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Reports;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>
/// Maps the report aggregate.
/// <para>
/// <b>The unique index on (subject type, subject, reporter) is the de-duplication rule made
/// physical.</b> Without it one determined member could flag anything by reporting it five times,
/// which is precisely the abuse an auto-flag threshold invites.
/// </para>
/// <para>
/// Not soft-deletable and never removed: a report is the record of a decision, and a platform that
/// could not show why it hid something has no answer for the organizer who asks.
/// </para>
/// </summary>
public sealed class CommunityLifeReportConfiguration
    : IEntityTypeConfiguration<CommunityLifeReport>
{
    public void Configure(EntityTypeBuilder<CommunityLifeReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityLifePersistenceConstants.ReportsTable,
            CommunityLifeModuleMetadata.Schema);

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id)
            .HasConversion(CommunityLifeValueConverters.Id<CommunityLifeReportId>());

        builder.Property(report => report.ReportedByUserId)
            .HasConversion(CommunityLifeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(report => report.ResolvedByUserId)
            .HasConversion(CommunityLifeValueConverters.Id<UserId>());

        builder.Property(report => report.SubjectType)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(report => report.Reason)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(report => report.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(report => report.Detail)
            .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

        builder.Property(report => report.ResolutionNote)
            .HasMaxLength(CommunityLifeLimits.MaxModerationNote);

        // One report per member per subject. See the class summary.
        builder.HasIndex(report =>
                new { report.SubjectType, report.SubjectId, report.ReportedByUserId })
            .IsUnique()
            .HasDatabaseName("ix_community_life_reports_unique");

        // The count that drives the auto-flag.
        builder.HasIndex(report => new { report.SubjectType, report.SubjectId })
            .HasDatabaseName("ix_community_life_reports_subject");

        // The moderation queue, oldest unresolved first.
        builder.HasIndex(report => new { report.Status, report.CreatedAt })
            .HasDatabaseName("ix_community_life_reports_queue");

        builder.Ignore(report => report.DomainEvents);
        builder.Ignore(report => report.IsResolved);
        builder.Ignore(report => report.FlagsImmediately);
    }
}
