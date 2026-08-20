using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Housing.Persistence.Configurations;

/// <summary>
/// Maps the viewing aggregate and its history — two tables.
/// <para>
/// <b>The index on (listing, seeker, status) is the address-disclosure check</b>, and it runs on
/// every listing detail render for a signed-in member. It is the most-executed query in the module
/// and the one most worth an index: without it, deciding whether to show a street name means
/// scanning a member's whole viewing history.
/// </para>
/// <para>
/// <b>The index on the confirmed slot is the expiry sweep's work list.</b> A viewing nobody expires
/// stays open forever and keeps counting against the member's open-viewing bound.
/// </para>
/// </summary>
public sealed class HousingViewingConfiguration : IEntityTypeConfiguration<HousingViewing>
{
    public void Configure(EntityTypeBuilder<HousingViewing> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            HousingPersistenceConstants.ViewingsTable, HousingModuleMetadata.Schema);

        builder.HasKey(viewing => viewing.Id);

        builder.Property(viewing => viewing.Id)
            .HasConversion(HousingValueConverters.Id<HousingViewingId>());

        builder.Property(viewing => viewing.ListingId)
            .HasConversion(HousingValueConverters.Id<HousingListingId>())
            .IsRequired();

        builder.Property(viewing => viewing.LandlordId)
            .HasConversion(HousingValueConverters.Id<LandlordProfileId>())
            .IsRequired();

        builder.Property(viewing => viewing.SeekerUserId)
            .HasConversion(HousingValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(viewing => viewing.Status)
            .HasConversion<string>()
            .HasMaxLength(HousingPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(viewing => viewing.Message)
            .HasMaxLength(HousingLimits.MaxViewingNote);

        builder.Property(viewing => viewing.ClosureReason)
            .HasMaxLength(HousingLimits.MaxViewingNote);

        builder.Property(viewing => viewing.ClosedBy)
            .HasConversion(HousingValueConverters.Id<UserId>());

        ConfigureWindow(builder);
        ConfigureHistory(builder);

        // The address-disclosure check. See the class summary.
        builder.HasIndex(viewing =>
                new { viewing.ListingId, viewing.SeekerUserId, viewing.Status })
            .HasDatabaseName("ix_housing_viewings_listing_seeker");

        builder.HasIndex(viewing => new { viewing.LandlordId, viewing.Status })
            .HasDatabaseName("ix_housing_viewings_landlord_status");

        builder.HasIndex(viewing => new { viewing.SeekerUserId, viewing.Status })
            .HasDatabaseName("ix_housing_viewings_seeker_status");

        builder.Ignore(viewing => viewing.DomainEvents);
        builder.Ignore(viewing => viewing.IsOpen);
        builder.Ignore(viewing => viewing.UnlocksAddress);
        builder.Ignore(viewing => viewing.SatisfiesReservationRequirement);
    }

    private static void ConfigureWindow(EntityTypeBuilder<HousingViewing> builder)
    {
        builder.OwnsOne(viewing => viewing.Window, window =>
        {
            window.Property(value => value.PreferredAt).HasColumnName("preferred_at");
            window.Property(value => value.ConfirmedAt).HasColumnName("confirmed_at");

            window.Property(value => value.DurationMinutes)
                .HasColumnName("duration_minutes")
                .IsRequired();

            window.Property(value => value.IsVirtual).HasColumnName("is_virtual");

            window.Property(value => value.Place)
                .HasColumnName("meeting_place")
                .HasMaxLength(HousingLimits.MaxViewingNote);

            window.Ignore(value => value.EffectiveAt);
            window.Ignore(value => value.EndsAt);

            // The expiry sweep's work list, ordered. Owned column, so the index lives here.
            window.HasIndex(value => value.ConfirmedAt)
                .HasDatabaseName("ix_housing_viewings_confirmed_at");
        });

        builder.Navigation(viewing => viewing.Window).IsRequired();
    }

    private static void ConfigureHistory(EntityTypeBuilder<HousingViewing> builder) =>
        builder.OwnsMany(viewing => viewing.History, entry =>
        {
            entry.ToTable(
                HousingPersistenceConstants.ViewingHistoryTable, HousingModuleMetadata.Schema);

            entry.HasKey(value => value.EntryId);

            // Caller-supplied key. Without this EF emits an UPDATE instead of an INSERT.
            entry.Property(value => value.EntryId).ValueGeneratedNever();

            entry.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(HousingPersistenceConstants.EnumLength)
                .IsRequired();

            entry.Property(value => value.ActorId)
                .HasConversion(HousingValueConverters.Id<UserId>());

            entry.Property(value => value.Reason).HasMaxLength(HousingLimits.MaxViewingNote);

            entry.Property(value => value.OccurredAt).IsRequired();
        });
}
