using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.CommunityLife.Persistence.Configurations;

/// <summary>
/// Maps the membership aggregate and its history.
/// <para>
/// <b>The unique index on (organization, member) is the module's most important constraint.</b> It
/// is what makes "you are already a member" a database fact rather than a race between two taps on
/// a join button — and it is why the aggregate keeps <c>Left</c> and <c>Removed</c> as statuses
/// rather than deleting the row: a second row for a returning member would violate it, which is
/// exactly the outcome wanted.
/// </para>
/// <para>
/// Not soft-deletable, deliberately. A membership that vanished would erase the history that makes
/// readmission decisions possible.
/// </para>
/// </summary>
public sealed class OrganizationMembershipConfiguration
    : IEntityTypeConfiguration<OrganizationMembership>
{
    public void Configure(EntityTypeBuilder<OrganizationMembership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityLifePersistenceConstants.MembershipsTable,
            CommunityLifeModuleMetadata.Schema);

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Revision).IsConcurrencyToken();

        builder.Property(membership => membership.Id)
            .HasConversion(CommunityLifeValueConverters.Id<OrganizationMembershipId>());

        builder.Property(membership => membership.OrganizationId)
            .HasConversion(CommunityLifeValueConverters.Id<OrganizationId>())
            .IsRequired();

        builder.Property(membership => membership.MemberUserId)
            .HasConversion(CommunityLifeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(membership => membership.InvitedBy)
            .HasConversion(CommunityLifeValueConverters.Id<UserId>());

        builder.Property(membership => membership.DecidedBy)
            .HasConversion(CommunityLifeValueConverters.Id<UserId>());

        builder.Property(membership => membership.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(membership => membership.Role)
            .HasConversion<string>()
            .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(membership => membership.RequestMessage)
            .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

        builder.OwnsMany(membership => membership.History, entry =>
        {
            entry.ToTable(
                CommunityLifePersistenceConstants.MembershipHistoryTable,
                CommunityLifeModuleMetadata.Schema);

            entry.HasKey(value => value.EntryId);

            // Caller-supplied key. Without this EF emits an UPDATE instead of an INSERT and it
            // surfaces as a bogus concurrency conflict rather than a missing row.
            entry.Property(value => value.EntryId).ValueGeneratedNever();

            entry.Property(value => value.Status)
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            entry.Property(value => value.Role)
                .HasConversion<string>()
                .HasMaxLength(CommunityLifePersistenceConstants.EnumLength)
                .IsRequired();

            entry.Property(value => value.ChangedBy)
                .HasConversion(CommunityLifeValueConverters.Id<UserId>());

            entry.Property(value => value.Reason)
                .HasMaxLength(CommunityLifeLimits.MaxNoteLength);

            entry.Property(value => value.OccurredAt).IsRequired();
        });

        // One membership per member per organization, whatever its status. See the class summary.
        builder.HasIndex(membership =>
                new { membership.OrganizationId, membership.MemberUserId })
            .IsUnique()
            .HasDatabaseName("ix_community_life_memberships_unique");

        // The member list, filtered by status. The most-run membership query.
        builder.HasIndex(membership =>
                new { membership.OrganizationId, membership.Status, membership.Role })
            .HasDatabaseName("ix_community_life_memberships_roster");

        // "Which organizations do I belong to" — rendered on every organizer screen.
        builder.HasIndex(membership => new { membership.MemberUserId, membership.Status })
            .HasDatabaseName("ix_community_life_memberships_member");

        // The lapse sweep's work list. Without it, finding memberships that expired today means
        // reading every membership ever created.
        builder.HasIndex(membership => membership.RenewsAt)
            .HasDatabaseName("ix_community_life_memberships_renews_at");

        builder.Ignore(membership => membership.DomainEvents);
        builder.Ignore(membership => membership.IsCurrent);
        builder.Ignore(membership => membership.IsPending);
        builder.Ignore(membership => membership.CanOrganize);
        builder.Ignore(membership => membership.CanAdminister);
        builder.Ignore(membership => membership.CanTakeAttendance);
    }
}
