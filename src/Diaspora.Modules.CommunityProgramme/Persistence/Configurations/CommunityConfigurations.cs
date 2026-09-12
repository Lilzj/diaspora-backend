using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// `Diaspora.Modules.Community` is a namespace in scope here, so the bare type name resolves to
// it. Aliased rather than renaming the aggregate: the domain word is right, and the collision
// is an artefact of a sibling module that models the social graph rather than the groups.
using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.Modules.CommunityProgramme.Persistence.Configurations;

/// <summary>
/// Maps the community itself.
/// <para>
/// <b>The slug carries a unique index and it is not optional.</b> <see cref="CommunitySlug"/>
/// guarantees the shape of a handle and nothing about its uniqueness — the type cannot see the other
/// rows. Without this index two communities could take the same handle and one of them would
/// silently stop resolving, which is the failure the whole stable-slug requirement exists to
/// prevent.
/// </para>
/// <para>
/// <b>The country and presence codes are indexed because the directory's central filter is
/// origin.</b> A directory that scans the table to answer "Nigerians anywhere" is a directory that
/// stops working at the scale §77 requires it to survive.
/// </para>
/// <para>
/// <b>Presence and languages are stored as primitive collections rather than owned tables.</b> They
/// are lists of two-letter codes with no identity of their own, nothing references a single entry,
/// and no query needs to join to one — an owned table would buy a join and a shadow key for nothing.
/// </para>
/// </summary>
public sealed class CommunityConfiguration : IEntityTypeConfiguration<CommunityAggregate>
{
    public void Configure(EntityTypeBuilder<CommunityAggregate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CommunitiesTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(community => community.Id);

        builder.Property(community => community.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityId>())
            .ValueGeneratedNever();

        // The handle. Converted to its string rather than owned: it is one column, and a value
        // object with one component that needs an index reads better as a column than as a table.
        builder.Property(community => community.Slug)
            .HasConversion(
                slug => slug.Value,
                value => CommunitySlug.Create(value).Value)
            .HasMaxLength(CommunitySlug.MaximumLength)
            .IsRequired();

        builder.HasIndex(community => community.Slug)
            .IsUnique()
            .HasDatabaseName("ix_communities_slug");

        builder.Property(community => community.Name)
            .HasMaxLength(CommunityAggregate.NameMaximumLength)
            .IsRequired();

        builder.Property(community => community.Tagline)
            .HasMaxLength(CommunityAggregate.TaglineMaximumLength);

        builder.Property(community => community.Description)
            .HasMaxLength(CommunityAggregate.DescriptionMaximumLength);

        // Stored as their names. An integer would be smaller and would make every migration that
        // reorders the enum a silent data corruption.
        builder.Property(community => community.Type).HasConversion<string>().HasMaxLength(32);
        builder.Property(community => community.Status).HasConversion<string>().HasMaxLength(32);
        builder.Property(community => community.Visibility).HasConversion<string>().HasMaxLength(32);

        builder.Property(community => community.OriginCountryCode).HasMaxLength(2);
        builder.Property(community => community.OriginRegionName).HasMaxLength(120);
        builder.Property(community => community.PrimaryLanguage).HasMaxLength(32);
        builder.Property(community => community.StatusReason).HasMaxLength(500);

        // Display metadata. Unindexed: nothing filters, sorts or joins on any of them.
        builder.Property(community => community.IconName).HasMaxLength(64);
        builder.Property(community => community.AccentColor).HasMaxLength(7);

        builder.PrimitiveCollection(community => community.PresenceCountryCodes);
        builder.PrimitiveCollection(community => community.SupportedLanguages);

        builder.Property(community => community.MemberCount).IsRequired();

        // The largest milestone ever reached, so each is announced once. Backfilled by the
        // migration that adds it: a community already at 150 members has already passed 100.
        builder.Property(community => community.HighestMemberMilestone).IsRequired();

        builder.Property(community => community.RequiresApproval).IsRequired();

        // The directory pages on these three together.
        builder.HasIndex(community => new { community.Status, community.Visibility })
            .HasDatabaseName("ix_communities_status_visibility");

        builder.HasIndex(community => community.OriginCountryCode)
            .HasDatabaseName("ix_communities_origin_country");

        builder.HasIndex(community => community.Type)
            .HasDatabaseName("ix_communities_type");
    }
}

/// <summary>
/// Maps one person's standing in one community.
/// <para>
/// <b>One row per member per community, enforced by a unique index.</b> The aggregate keeps a single
/// row through leaving and rejoining so a revocation cannot be laundered by leaving; that only holds
/// if the store refuses a second row. Without this index a revoked member rejoins by creating a
/// fresh row and the domain rule becomes decorative.
/// </para>
/// <para>
/// <b>At most one primary per member, and the database says so.</b> A filtered unique index over
/// <c>MemberId</c> where <c>IsPrimary</c> — the aggregate cannot enforce this, because a membership
/// cannot see the member's other memberships. Two primaries would make "your community" ambiguous
/// on a profile, and nothing above this layer is in a position to prevent it.
/// </para>
/// </summary>
public sealed class CommunityMembershipConfiguration : IEntityTypeConfiguration<CommunityMembership>
{
    public void Configure(EntityTypeBuilder<CommunityMembership> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CommunityMembershipsTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityMembershipId>())
            .ValueGeneratedNever();

        builder.Property(membership => membership.CommunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityId>())
            .IsRequired();

        builder.Property(membership => membership.MemberId)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(membership => membership.DecidedBy)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>());

        builder.Property(membership => membership.State).HasConversion<string>().HasMaxLength(32);

        // Private is the enum's zero, so a row written without this column reads as private rather
        // than as consent to publish. See CommunityMembershipVisibility.
        builder.Property(membership => membership.Visibility)
            .HasConversion<string>()
            .HasMaxLength(32);

        // A list of role names with no identity of their own. Nothing joins to a single entry, so
        // an owned table would buy a join and a shadow key for nothing.
        builder.PrimitiveCollection(membership => membership.Roles);

        builder.Property(membership => membership.DecisionReason)
            .HasMaxLength(CommunityMembership.ReasonMaximumLength);

        builder.Property(membership => membership.Revision).IsConcurrencyToken();

        builder.HasIndex(membership => new { membership.CommunityId, membership.MemberId })
            .IsUnique()
            .HasDatabaseName("ix_community_memberships_community_member");

        // Answers "which communities am I in" without scanning, which every directory read does
        // once per page to fill in the caller's own membershipState.
        builder.HasIndex(membership => new { membership.MemberId, membership.State })
            .HasDatabaseName("ix_community_memberships_member_state");

        builder.HasIndex(membership => membership.MemberId)
            .IsUnique()
            // A bare boolean predicate rather than `= TRUE`: valid on Postgres, which the
            // deployment runs, and on SQLite, which the integration suite runs.
            .HasFilter("\"IsPrimary\"")
            .HasDatabaseName("ix_community_memberships_one_primary_per_member");

        // The count on the community is maintained by the aggregate; this is the relationship that
        // lets a membership be loaded with its community in one round trip.
        builder.HasOne<CommunityAggregate>()
            .WithMany()
            .HasForeignKey(membership => membership.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// A request to close a community — 11 Sep 2026.
/// <para>
/// <b>Rows are kept after the request is over.</b> A community that was nearly closed and kept is a
/// fact worth reading a year later, and a request that was carried out is the record of why a
/// community that people remember is no longer there.
/// </para>
/// <para>
/// <b>The deadline is indexed because a sweep asks for it every fifteen minutes.</b> "Which approved
/// requests has the wait run out on" is the one query that runs whether or not anybody is looking.
/// </para>
/// </summary>
public sealed class CommunityDeletionRequestConfiguration
    : IEntityTypeConfiguration<CommunityDeletionRequest>
{
    public void Configure(EntityTypeBuilder<CommunityDeletionRequest> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CommunityDeletionRequestsTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityDeletionRequestId>())
            .ValueGeneratedNever();

        builder.Property(request => request.CommunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityId>())
            .IsRequired();

        builder.Property(request => request.RequestedBy)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(request => request.ReviewedBy)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>());

        builder.Property(request => request.Reason)
            .HasMaxLength(CommunityDeletionRequest.ReasonMaximumLength)
            .IsRequired();

        builder.Property(request => request.DecisionReason)
            .HasMaxLength(CommunityDeletionRequest.ReasonMaximumLength);

        // As a string, for the reason every enum in this module is stored that way: reordering the
        // enum must never silently reassign what a stored row means.
        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(request => request.RequestedAt).IsRequired();
        builder.Property(request => request.EffectiveAt).IsRequired();

        builder.HasIndex(request => new { request.CommunityId, request.Status })
            .HasDatabaseName("ix_community_deletion_requests_community_status");

        builder.HasIndex(request => new { request.Status, request.EffectiveAt })
            .HasDatabaseName("ix_community_deletion_requests_status_effective");

        builder.HasOne<CommunityAggregate>()
            .WithMany()
            .HasForeignKey(request => request.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// An offer to hand a community over — 11 Sep 2026.
/// <para>
/// Indexed from both sides, because both sides ask: the community, for "is there an offer out on
/// this one", and the person offered it, for "does anybody want me to take over theirs".
/// </para>
/// </summary>
public sealed class CommunityOwnershipTransferConfiguration
    : IEntityTypeConfiguration<CommunityOwnershipTransfer>
{
    public void Configure(EntityTypeBuilder<CommunityOwnershipTransfer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityProgrammePersistenceConstants.CommunityOwnershipTransfersTable,
            CommunityProgrammeModuleMetadata.Schema);

        builder.HasKey(transfer => transfer.Id);

        builder.Property(transfer => transfer.Id)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityOwnershipTransferId>())
            .ValueGeneratedNever();

        builder.Property(transfer => transfer.CommunityId)
            .HasConversion(CommunityProgrammeValueConverters.Id<CommunityId>())
            .IsRequired();

        builder.Property(transfer => transfer.OfferedBy)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(transfer => transfer.OfferedTo)
            .HasConversion(CommunityProgrammeValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(transfer => transfer.Message)
            .HasMaxLength(CommunityOwnershipTransfer.MessageMaximumLength);

        builder.Property(transfer => transfer.DeclineReason)
            .HasMaxLength(CommunityOwnershipTransfer.MessageMaximumLength);

        builder.Property(transfer => transfer.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(transfer => transfer.OfferedAt).IsRequired();
        builder.Property(transfer => transfer.ExpiresAt).IsRequired();

        builder.HasIndex(transfer => new { transfer.CommunityId, transfer.Status })
            .HasDatabaseName("ix_community_ownership_transfers_community_status");

        builder.HasIndex(transfer => new { transfer.OfferedTo, transfer.Status })
            .HasDatabaseName("ix_community_ownership_transfers_offered_to_status");

        builder.HasIndex(transfer => new { transfer.Status, transfer.ExpiresAt })
            .HasDatabaseName("ix_community_ownership_transfers_status_expiry");

        builder.HasOne<CommunityAggregate>()
            .WithMany()
            .HasForeignKey(transfer => transfer.CommunityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
