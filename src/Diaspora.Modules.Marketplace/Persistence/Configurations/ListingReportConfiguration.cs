using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Reports;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps listing reports.
/// <para>
/// <b>Unique on (listing, reporter), which is what keeps the report count honest.</b> The number of
/// distinct members who reported a listing is what decides how urgently a moderator looks; without
/// the constraint one determined member could raise it to fifty on their own.
/// </para>
/// </summary>
public sealed class ListingReportConfiguration : IEntityTypeConfiguration<ListingReport>
{
    public void Configure(EntityTypeBuilder<ListingReport> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.ReportsTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(report => report.Id);

        builder.Property(report => report.Id)
            .HasConversion(MarketplaceValueConverters.Id<ListingReportId>());

        builder.Property(report => report.ListingId)
            .HasConversion(MarketplaceValueConverters.Id<MarketplaceListingId>())
            .IsRequired();

        builder.Property(report => report.SellerId)
            .HasConversion(MarketplaceValueConverters.Id<SellerProfileId>())
            .IsRequired();

        builder.Property(report => report.ReporterId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(report => report.ReviewerId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>());

        builder.Property(report => report.Reason)
            .HasConversion<string>()
            .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(report => report.Status)
            .HasConversion<string>()
            .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(report => report.Detail)
            .HasMaxLength(MarketplaceLimits.MaxReportNote);

        builder.Property(report => report.Resolution)
            .HasMaxLength(MarketplaceLimits.MaxReportNote);

        builder.HasIndex(report => new { report.ListingId, report.ReporterId })
            .IsUnique()
            .HasDatabaseName("ux_marketplace_reports_listing_reporter");

        // The moderation queue: open first, worst reason first, newest within that.
        builder.HasIndex(report => new { report.Status, report.Reason, report.CreatedAt })
            .HasDatabaseName("ix_marketplace_reports_queue");

        builder.HasIndex(report => report.SellerId)
            .HasDatabaseName("ix_marketplace_reports_seller");

        builder.Ignore(report => report.DomainEvents);
        builder.Ignore(report => report.IsOpen);
    }
}
