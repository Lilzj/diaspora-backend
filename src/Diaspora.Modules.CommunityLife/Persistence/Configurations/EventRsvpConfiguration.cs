using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>
/// Maps the RSVP aggregate and its history.
/// <para>
/// <b>The unique index on (event, attendee) is what makes double-responding impossible.</b> Two
/// taps on a Going button race; without the constraint both would insert, both would settle the
/// event's counters, and the going count would be permanently two higher than the guest list.
/// Because it holds across withdrawn responses too, a member changing their mind twice moves the
/// row they already have — which is exactly what the aggregate's transition machine assumes.
/// </para>
/// <para>
/// <b>The waitlist index carries <c>CreatedAt</c> and that is not decoration.</b> Promoting whoever
/// the database returned first would make a waiting list a lottery; the order people joined it is
/// the only defensible one, and it has to be indexed or every promotion is a scan.
/// </para>
/// </summary>
public sealed class EventRsvpConfiguration : IEntityTypeConfiguration<EventRsvp>
{
    public void Configure(EntityTypeBuilder<EventRsvp> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityLifePersistenceConstants.RsvpsTable,
            CommunityLifeModuleMetadata.Schema);

        builder.HasKey(rsvp => rsvp.Id);

        builder.Property(rsvp => rsvp.Revision).IsConcurrencyToken();

        builder.Property(rsvp => rsvp.Id)
            .HasConversion(CommunityLifeValueConverters.Id<EventRsvpId>());

        builder.Property(rsvp => rsvp.EventId)
            .HasConversion(CommunityLifeValueConverters.Id<CommunityEventId>())
            .IsRequired();

        builder.Property(rsvp => rsvp.OrganizationId)
            .HasConversion(CommunityLifeValueConverters.Id<OrganizationId>());

        builder.Property(rsvp => rsvp.AttendeeUserId)
            .HasConversion(CommunityLifeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(rsvp => rsvp.CheckedInBy)
            .HasConversion(CommunityLifeValueConverters.Id<UserId>());

        builder.Property(rsvp => rsvp.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(rsvp => rsvp.DeclineReason)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(rsvp => rsvp.CheckInMethod)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength);

        builder.Property(rsvp => rsvp.Note).HasMaxLength(CommunityLifeLimits.MaxNoteLength);

        builder.OwnsMany(rsvp => rsvp.History, entry =>
        {
            entry.ToTable(
                CommunityLifePersistenceConstants.RsvpHistoryTable,
                CommunityLifeModuleMetadata.Schema);

            entry.HasKey(value => value.EntryId);
            entry.Property(value => value.EntryId).ValueGeneratedNever();

            entry.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            entry.Property(value => value.ChangedBy)
                .HasConversion(CommunityLifeValueConverters.Id<UserId>());

            entry.Property(value => value.Reason)
                .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

            entry.Property(value => value.OccurredAt).IsRequired();
        });

        // One response per member per event, whatever its status. See the class summary.
        builder.HasIndex(rsvp => new { rsvp.EventId, rsvp.AttendeeUserId })
            .IsUnique()
            .HasDatabaseName("ix_community_life_rsvps_unique");

        // The attendee list and the check-in screen, filtered by status.
        builder.HasIndex(rsvp => new { rsvp.EventId, rsvp.Status })
            .HasDatabaseName("ix_community_life_rsvps_event_status");

        // The waiting list, in the order people joined it. See the class summary.
        builder.HasIndex(rsvp => new { rsvp.EventId, rsvp.Status, rsvp.CreatedAt })
            .HasDatabaseName("ix_community_life_rsvps_waitlist");

        // "What am I going to" — the busiest read on a member's own screen.
        builder.HasIndex(rsvp => new { rsvp.AttendeeUserId, rsvp.Status })
            .HasDatabaseName("ix_community_life_rsvps_attendee");

        // Everything anybody responded to across one organization.
        builder.HasIndex(rsvp => new { rsvp.OrganizationId, rsvp.Status })
            .HasDatabaseName("ix_community_life_rsvps_organization");

        builder.Ignore(rsvp => rsvp.DomainEvents);
        builder.Ignore(rsvp => rsvp.HoldsCapacity);
        builder.Ignore(rsvp => rsvp.IsCheckedIn);
        builder.Ignore(rsvp => rsvp.IsTerminal);
        builder.Ignore(rsvp => rsvp.HeldPlaces);
    }
}
