using Diaspora.Modules.Community.Domain.Follows;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps the follow graph.</summary>
public sealed class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(CommunityPersistenceConstants.FollowsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(follow => follow.Id);

        builder.Property(follow => follow.Id)
            .HasConversion(CommunityValueConverters.Id<FollowId>());

        builder.Property(follow => follow.FollowerId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(follow => follow.TargetType)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(follow => follow.EdgeKey)
            .HasMaxLength(CommunityPersistenceConstants.CompositeKeyLength)
            .IsRequired();

        builder.HasIndex(follow => follow.EdgeKey)
            .IsUnique()
            .HasDatabaseName("ix_follows_edge");

        // The following feed's first query: everything this member follows, of one kind.
        builder.HasIndex(follow => new { follow.FollowerId, follow.TargetType })
            .HasDatabaseName("ix_follows_follower_type");

        // The follower count, and the follower list on a profile.
        builder.HasIndex(follow => new { follow.TargetType, follow.TargetId })
            .HasDatabaseName("ix_follows_target");

        builder.Ignore(follow => follow.DomainEvents);
    }
}
