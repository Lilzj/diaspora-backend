using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Marketplace.Persistence.Configurations;

/// <summary>
/// Maps the reservation aggregate and its history — two tables.
/// <para>
/// <b>The index on the collection window is the expiry sweep's whole work list.</b> A reservation
/// nobody expires is stock nobody can buy, and a sweep that had to scan every reservation ever made
/// to find the lapsed ones would get slower exactly as the marketplace got busier.
/// </para>
/// </summary>
public sealed class ListingReservationConfiguration
    : IEntityTypeConfiguration<ListingReservation>
{
    public void Configure(EntityTypeBuilder<ListingReservation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            MarketplacePersistenceConstants.ReservationsTable, MarketplaceModuleMetadata.Schema);

        builder.HasKey(reservation => reservation.Id);

        builder.Property(reservation => reservation.Id)
            .HasConversion(MarketplaceValueConverters.Id<ListingReservationId>());

        builder.Property(reservation => reservation.ListingId)
            .HasConversion(MarketplaceValueConverters.Id<MarketplaceListingId>())
            .IsRequired();

        builder.Property(reservation => reservation.SellerId)
            .HasConversion(MarketplaceValueConverters.Id<SellerProfileId>())
            .IsRequired();

        builder.Property(reservation => reservation.BuyerUserId)
            .HasConversion(MarketplaceValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(reservation => reservation.OfferId)
            .HasConversion(MarketplaceValueConverters.Id<ListingOfferId>());

        builder.Property(reservation => reservation.Status)
            .HasConversion<string>()
            .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(reservation => reservation.Quantity).IsRequired();

        builder.Property(reservation => reservation.AgreedAmount)
            .HasColumnType(MarketplacePersistenceConstants.MoneyColumnType);

        builder.Property(reservation => reservation.CurrencyCode)
            .HasMaxLength(MarketplacePersistenceConstants.CurrencyCodeLength);

        builder.Property(reservation => reservation.ClosedBy)
            .HasConversion(MarketplaceValueConverters.Id<UserId>());

        builder.Property(reservation => reservation.ClosureReason)
            .HasMaxLength(MarketplaceLimits.MaxReservationNote);

        ConfigureWindow(builder);
        ConfigureHistory(builder);

        // The buyer's live reservation on a listing: what stops one buyer holding one item twice.
        builder.HasIndex(reservation =>
                new { reservation.ListingId, reservation.BuyerUserId, reservation.Status })
            .HasDatabaseName("ix_marketplace_reservations_listing_buyer");

        builder.HasIndex(reservation => new { reservation.SellerId, reservation.Status })
            .HasDatabaseName("ix_marketplace_reservations_seller_status");

        builder.HasIndex(reservation => new { reservation.BuyerUserId, reservation.Status })
            .HasDatabaseName("ix_marketplace_reservations_buyer_status");

        builder.Ignore(reservation => reservation.DomainEvents);
        builder.Ignore(reservation => reservation.IsHoldingStock);
    }

    private static void ConfigureWindow(EntityTypeBuilder<ListingReservation> builder)
    {
        builder.OwnsOne(reservation => reservation.Window, window =>
        {
            window.Property(value => value.From).HasColumnName("collect_from");
            window.Property(value => value.Until).HasColumnName("collect_until");

            window.Property(value => value.Place)
                .HasColumnName("collection_place")
                .HasMaxLength(MarketplaceLimits.MaxReservationNote);

            // The expiry sweep's work list, ordered. Owned column, so the index lives here.
            window.HasIndex(value => value.Until)
                .HasDatabaseName("ix_marketplace_reservations_collect_until");
        });

        builder.Navigation(reservation => reservation.Window).IsRequired();
    }

    private static void ConfigureHistory(EntityTypeBuilder<ListingReservation> builder) =>
        builder.OwnsMany(reservation => reservation.History, entry =>
        {
            entry.ToTable(
                MarketplacePersistenceConstants.ReservationHistoryTable,
                MarketplaceModuleMetadata.Schema);

            entry.HasKey(value => value.EventId);
            entry.Property(value => value.EventId).ValueGeneratedNever();

            entry.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(MarketplacePersistenceConstants.EnumLength)
                .IsRequired();

            entry.Property(value => value.ActorId)
                .HasConversion(MarketplaceValueConverters.Id<UserId>());

            entry.Property(value => value.Reason)
                .HasMaxLength(MarketplaceLimits.MaxReservationNote);

            entry.Property(value => value.OccurredAt).IsRequired();
        });
}
