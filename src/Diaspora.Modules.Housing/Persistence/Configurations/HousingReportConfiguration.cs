using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps housing reports.
/// <para>
/// <b>The unique index on (listing, reporter) is one report each.</b> Without it a single member
/// could drive a listing past the auto-flag threshold on their own, which turns the safety
/// mechanism into a takedown button any competitor can press.
/// </para>
/// <para>
/// Not soft-deletable: a report is a record of a decision, and the trail of what was reported and
/// what was done about it is what an appeal and an audit are answered from.
/// </para>
/// </summary>
public sealed class HousingReportConfiguration : IEntityTypeConfiguration<HousingReport>
{
    public void Configure(EntityTypeBuilder<HousingReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(HousingPersistenceConstants.ReportsTable, HousingModuleMetadata.Schema);

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id)
            .HasConversion(HousingValueConverters.Id<HousingReportId>());

        builder.Property(report => report.ListingId)
            .HasConversion(HousingValueConverters.Id<HousingListingId>())
            .IsRequired();

        builder.Property(report => report.LandlordId)
            .HasConversion(HousingValueConverters.Id<LandlordProfileId>())
            .IsRequired();

        builder.Property(report => report.ReportedBy)
            .HasConversion(HousingValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(report => report.ResolvedBy)
            .HasConversion(HousingValueConverters.Id<UserId>());

        builder.Property(report => report.Reason)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(report => report.Status)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(report => report.Detail).HasMaxLength(HousingLimits.MaxReportDetail);

        builder.Property(report => report.Resolution)
            .HasMaxLength(HousingLimits.MaxResolutionNote);

        // One report each. See the class summary.
        builder.HasIndex(report => new { report.ListingId, report.ReportedBy })
            .IsUnique()
            .HasDatabaseName("ix_housing_reports_listing_reporter");

        // The moderation queue, in the order it is worked.
        builder.HasIndex(report => new { report.Status, report.Reason })
            .HasDatabaseName("ix_housing_reports_status_reason");

        // A landlord's record: what else has been reported against them.
        builder.HasIndex(report => new { report.LandlordId, report.Status })
            .HasDatabaseName("ix_housing_reports_landlord_status");

        builder.Ignore(report => report.DomainEvents);
        builder.Ignore(report => report.IsOpen);
        builder.Ignore(report => report.FlagsImmediately);
    }
}
