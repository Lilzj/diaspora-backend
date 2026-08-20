using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps the negotiation aggregate and its append-only history — two tables.
/// <para>
/// <b>Keyed by listing and buyer in an index rather than in the primary key.</b> A buyer may
/// negotiate on one listing many times over its life — offer, withdraw, offer again — so the pair
/// is not unique. What must be unique is that only one of them is <em>open</em>, and that is a
/// rule the handler enforces against this index rather than a constraint the database can state.
/// </para>
/// </summary>
public sealed class ListingOfferConfiguration : IEntityTypeConfiguration<ListingOffer>
{
    public void Configure(EntityTypeBuilder<ListingOffer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.OffersTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(offer => offer.Id);

        builder.Property(offer => offer.Id)
            .HasConversion(MarketplaceValueConverters.Id<ListingOfferId>());

        builder.Property(offer => offer.ListingId)
            .HasConversion(MarketplaceValueConverters.Id<MarketplaceListingId>())
            .IsRequired();

        builder.Property(offer => offer.SellerId)
            .HasConversion(MarketplaceValueConverters.Id<SellerProfileId>())
            .IsRequired();

        builder.Property(offer => offer.BuyerUserId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(offer => offer.SellerUserId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(offer => offer.Status)
            .HasConversion<string>()
            .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(offer => offer.AwaitingParty)
            .HasConversion<string>()
            .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(offer => offer.SettledBy)
            .HasConversion(MarketplaceValueConverters.Id<UserId>());

        builder.Property(offer => offer.SettlementReason)
            .HasMaxLength(MarketplaceLimits.MaxOfferMessage);

        ConfigureTerms(builder);
        ConfigureHistory(builder);

        // The buyer's open negotiation on a listing, which is the read every offer command starts
        // with.
        builder.HasIndex(offer => new { offer.ListingId, offer.BuyerUserId, offer.Status })
            .HasDatabaseName("ix_marketplace_offers_listing_buyer");

        // The seller dashboard: what is waiting on me.
        builder.HasIndex(offer => new { offer.SellerId, offer.Status, offer.AwaitingParty })
            .HasDatabaseName("ix_marketplace_offers_seller_status");

        // The buyer dashboard.
        builder.HasIndex(offer => new { offer.BuyerUserId, offer.Status })
            .HasDatabaseName("ix_marketplace_offers_buyer_status");

        // The expiry sweep's work list.
        builder.HasIndex(offer => offer.ExpiresAt)
            .HasDatabaseName("ix_marketplace_offers_expires_at");

        builder.Ignore(offer => offer.DomainEvents);
        builder.Ignore(offer => offer.IsOpen);
        builder.Ignore(offer => offer.Rounds);
        builder.Ignore(offer => offer.Duration);
    }

    private static void ConfigureTerms(EntityTypeBuilder<ListingOffer> builder)
    {
        builder.OwnsOne(offer => offer.Terms, terms =>
        {
            terms.Property(value => value.Amount)
                .HasColumnName("amount")
                .HasColumnType(MarketplacePersistenceConstants.MoneyColumnType)
                .IsRequired();

            terms.Property(value => value.CurrencyCode)
                .HasColumnName("currency")
                .HasMaxLength(MarketplacePersistenceConstants.CurrencyCodeLength)
                .IsRequired();

            terms.Property(value => value.Quantity).HasColumnName("quantity").IsRequired();

            terms.Property(value => value.Message)
                .HasColumnName("message")
                .HasMaxLength(MarketplaceLimits.MaxOfferMessage);

            terms.Ignore(value => value.UnitAmount);
        });

        builder.Navigation(offer => offer.Terms).IsRequired();
    }

    /// <summary>
    /// The negotiation history. Keyed on a caller-supplied identifier with
    /// <c>ValueGeneratedNever</c> — without it EF treats the non-default Guid as store-generated,
    /// emits an UPDATE instead of an INSERT, and surfaces as a bogus concurrency conflict.
    /// </summary>
    private static void ConfigureHistory(EntityTypeBuilder<ListingOffer> builder) =>
        builder.OwnsMany(offer => offer.History, entry =>
        {
            entry.ToTable(
                MarketplacePersistenceConstants.OfferHistoryTable,
                MarketplaceModuleMetadata.Schema);

            entry.HasKey(value => value.EntryId);
            entry.Property(value => value.EntryId).ValueGeneratedNever();

            entry.Property(value => value.Round).IsRequired();

            entry.Property(value => value.Action)
                .HasConversion<string>()
                .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
                .IsRequired();

            entry.Property(value => value.Party)
                .HasConversion<string>()
                .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
                .IsRequired();

            entry.Property(value => value.ActorId)
                .HasConversion(MarketplaceValueConverters.Id<UserId>());

            entry.Property(value => value.Amount)
                .HasColumnType(MarketplacePersistenceConstants.MoneyColumnType);

            entry.Property(value => value.CurrencyCode)
                .HasMaxLength(MarketplacePersistenceConstants.CurrencyCodeLength)
                .IsRequired();

            entry.Property(value => value.Quantity).IsRequired();

            entry.Property(value => value.Message)
                .HasMaxLength(MarketplaceLimits.MaxOfferMessage);

            entry.Property(value => value.OccurredAt).IsRequired();
        });
}
