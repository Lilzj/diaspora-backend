using Diaspora.Modules.Community.Domain.Hashtags;
using Diaspora.Modules.Community.Domain.Identifiers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps hashtags. Trending is an index on a stored window, not a live aggregate.</summary>
public sealed class HashtagConfiguration : IEntityTypeConfiguration<Hashtag>
{
    public void Configure(EntityTypeBuilder<Hashtag> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.HashtagsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(hashtag => hashtag.Id);

        builder.Property(hashtag => hashtag.Id)
            .HasConversion(CommunityValueConverters.Id<HashtagId>());

        builder.Property(hashtag => hashtag.Slug)
            .HasMaxLength(CommunityPersistenceConstants.SlugLength)
            .IsRequired();

        builder.Property(hashtag => hashtag.DisplayText)
            .HasMaxLength(CommunityPersistenceConstants.SlugLength)
            .IsRequired();

        builder.Property(hashtag => hashtag.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(hashtag => hashtag.PrimaryCountryCode)
            .HasMaxLength(CommunityPersistenceConstants.CountryCodeLength);

        builder.HasIndex(hashtag => hashtag.Slug)
            .IsUnique()
            .HasDatabaseName("ix_hashtags_slug");

        builder.HasIndex(hashtag => hashtag.RecentUsageCount)
            .HasDatabaseName("ix_hashtags_trending");

        builder.Ignore(hashtag => hashtag.DomainEvents);
        builder.Ignore(hashtag => hashtag.IsSurfaceable);
    }
}
