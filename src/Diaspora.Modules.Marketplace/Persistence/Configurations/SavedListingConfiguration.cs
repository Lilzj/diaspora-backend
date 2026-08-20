using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Saved;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps the watchlist. One row per (member, listing).
/// <para>
/// <b>The unique index is what makes saving idempotent in the database rather than by
/// convention.</b> A double-tap on a slow connection sends two saves; without the constraint the
/// member ends up watching one listing twice and the listing's save count is wrong forever.
/// </para>
/// <para>
/// <b>Hard-deleted, not soft.</b> Unsaving is the member saying "stop showing me this", and a
/// tombstone that kept the row would be the platform retaining exactly the browsing history they
/// asked it to forget. The aggregate raises its removal event before the row goes, which is how the
/// listing's count still comes down.
/// </para>
/// </summary>
public sealed class SavedListingConfiguration : IEntityTypeConfiguration<SavedListing>
{
    public void Configure(EntityTypeBuilder<SavedListing> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.SavedListingsTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(saved => saved.Id);

        builder.Property(saved => saved.Id)
            .HasConversion(MarketplaceValueConverters.Id<SavedListingId>());

        builder.Property(saved => saved.UserId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(saved => saved.ListingId)
            .HasConversion(MarketplaceValueConverters.Id<MarketplaceListingId>())
            .IsRequired();

        builder.Property(saved => saved.Note)
            .HasMaxLength(MarketplaceLimits.MaxNoteLength);

        builder.Property(saved => saved.NotifyOnPriceChange).IsRequired();

        builder.HasIndex(saved => new { saved.UserId, saved.ListingId })
            .IsUnique()
            .HasDatabaseName("ux_marketplace_saved_user_listing");

        // The price-drop fan-out reads by listing, filtered on the watch flag.
        builder.HasIndex(saved => new { saved.ListingId, saved.NotifyOnPriceChange })
            .HasDatabaseName("ix_marketplace_saved_listing_watchers");

        // The watchlist screen, newest first.
        builder.HasIndex(saved => new { saved.UserId, saved.CreatedAt })
            .HasDatabaseName("ix_marketplace_saved_user_created");

        builder.Ignore(saved => saved.DomainEvents);
    }
}
