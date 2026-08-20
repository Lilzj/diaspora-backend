using Diaspora.Modules.Community.Domain.Feeds;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps saved feed views.</summary>
public sealed class FeedSubscriptionConfiguration : IEntityTypeConfiguration<FeedSubscription>
{
    public void Configure(EntityTypeBuilder<FeedSubscription> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.FeedSubscriptionsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(subscription => subscription.Id);

        builder.Property(subscription => subscription.Id)
            .HasConversion(CommunityValueConverters.Id<FeedSubscriptionId>());

        builder.Property(subscription => subscription.UserId)
            .HasConversion(CommunityValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(subscription => subscription.FeedKey)
            .HasMaxLength(CommunityPersistenceConstants.SlugLength)
            .IsRequired();

        builder.Property(subscription => subscription.Label)
            .HasMaxLength(FeedSubscription.MaxLabelLength);

        builder.Property(subscription => subscription.RankingKey)
            .HasMaxLength(CommunityPersistenceConstants.SlugLength);

        builder.Property(subscription => subscription.Filters)
            .HasMaxLength(FeedSubscription.MaxFilterLength);

        builder.HasIndex(subscription => subscription.UserId)
            .HasDatabaseName("ix_feed_subscriptions_user");

        builder.Ignore(subscription => subscription.DomainEvents);
    }
}
