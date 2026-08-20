using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps the reservation aggregate and its history — two tables.
/// <para>
/// <b>The index on the hold expiry is the sweep's whole work list.</b> A reservation nobody expires
/// is a home nobody can let, and a sweep that had to scan every reservation ever made to find the
/// lapsed ones would get slower exactly as the platform got busier.
/// </para>
/// <para>
/// <b>The index on (listing, seeker, status) is what stops one member holding one room twice.</b>
/// It is checked before every reservation is opened, which on a hostel listing is the hot path.
/// </para>
/// </summary>
public sealed class HousingReservationConfiguration
    : IEntityTypeConfiguration<HousingReservation>
{
    public void Configure(EntityTypeBuilder<HousingReservation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.ReservationsTable, HousingModuleMetadata.Schema);

        builder.HasKey(reservation => reservation.Id);

        builder.Property(reservation => reservation.Id)
            .HasConversion(HousingValueConverters.Id<HousingReservationId>());

        builder.Property(reservation => reservation.ListingId)
            .HasConversion(HousingValueConverters.Id<HousingListingId>())
            .IsRequired();

        builder.Property(reservation => reservation.LandlordId)
            .HasConversion(HousingValueConverters.Id<LandlordProfileId>())
            .IsRequired();

        builder.Property(reservation => reservation.SeekerUserId)
            .HasConversion(HousingValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(reservation => reservation.ViewingId)
            .HasConversion(HousingValueConverters.Id<HousingViewingId>());

        builder.Property(reservation => reservation.Status)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(reservation => reservation.Units).IsRequired();

        builder.Property(reservation => reservation.Message)
            .HasMaxLength(HousingLimits.MaxReservationNote);

        builder.Property(reservation => reservation.ClosureReason)
            .HasMaxLength(HousingLimits.MaxReservationNote);

        builder.Property(reservation => reservation.ClosedBy)
            .HasConversion(HousingValueConverters.Id<UserId>());

        ConfigureTerms(builder);
        ConfigureHistory(builder);

        // What stops one member holding one room twice.
        builder.HasIndex(reservation =>
                new { reservation.ListingId, reservation.SeekerUserId, reservation.Status })
            .HasDatabaseName("ix_housing_reservations_listing_seeker");

        builder.HasIndex(reservation => new { reservation.LandlordId, reservation.Status })
            .HasDatabaseName("ix_housing_reservations_landlord_status");

        builder.HasIndex(reservation => new { reservation.SeekerUserId, reservation.Status })
            .HasDatabaseName("ix_housing_reservations_seeker_status");

        builder.Ignore(reservation => reservation.DomainEvents);
        builder.Ignore(reservation => reservation.HoldsUnits);
    }

    private static void ConfigureTerms(EntityTypeBuilder<HousingReservation> builder)
    {
        builder.OwnsOne(reservation => reservation.Terms, terms =>
        {
            terms.Property(value => value.MoveInDate).HasColumnName("move_in_date");
            terms.Property(value => value.MoveOutDate).HasColumnName("move_out_date");
            terms.Property(value => value.HoldExpiresAt).HasColumnName("hold_expires_at");

            terms.Property(value => value.AgreedRent)
                .HasColumnName("agreed_rent")
                .HasColumnType(HousingPersistenceConstants.MoneyColumnType);

            terms.Property(value => value.AgreedDeposit)
                .HasColumnName("agreed_deposit")
                .HasColumnType(HousingPersistenceConstants.MoneyColumnType);

            terms.Property(value => value.CurrencyCode)
                .HasColumnName("currency_code")
                .HasMaxLength(HousingPersistenceConstants.CurrencyCodeLength);

            terms.Property(value => value.Period)
                .HasColumnName("rent_period")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            terms.Property(value => value.LeaseType)
                .HasColumnName("lease_type")
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            terms.Property(value => value.Occupants).HasColumnName("occupants").IsRequired();

            terms.Ignore(value => value.StayNights);

            // The expiry sweep's work list, ordered. Owned column, so the index lives here.
            terms.HasIndex(value => value.HoldExpiresAt)
                .HasDatabaseName("ix_housing_reservations_hold_expires");
        });

        builder.Navigation(reservation => reservation.Terms).IsRequired();
    }

    private static void ConfigureHistory(EntityTypeBuilder<HousingReservation> builder) =>
        builder.OwnsMany(reservation => reservation.History, entry =>
        {
            entry.ToTable(
                HousingPersistenceConstants.ReservationHistoryTable,
                HousingModuleMetadata.Schema);

            entry.HasKey(value => value.EntryId);
            entry.Property(value => value.EntryId).ValueGeneratedNever();

            entry.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            entry.Property(value => value.ActorId)
                .HasConversion(HousingValueConverters.Id<UserId>());

            entry.Property(value => value.Reason).HasMaxLength(HousingLimits.MaxReservationNote);

            entry.Property(value => value.OccurredAt).IsRequired();
        });
}
