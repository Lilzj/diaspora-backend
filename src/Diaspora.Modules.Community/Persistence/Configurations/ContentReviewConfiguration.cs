using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Moderation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Community.Persistence.Configurations;

/// <summary>Maps automated reviews. The provider and version are stored for appeals.</summary>
public sealed class ContentReviewConfiguration : IEntityTypeConfiguration<ContentReview>
{
    public void Configure(EntityTypeBuilder<ContentReview> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            CommunityPersistenceConstants.ContentReviewsTable, CommunityModuleMetadata.Schema);

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id)
            .HasConversion(CommunityValueConverters.Id<ContentReviewId>());

        builder.Property(entity => entity.Status)
            .HasConversion<string>()
            .HasMaxLength(CommunityPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(entity => entity.Provider)
            .HasMaxLength(ContentReview.MaxProviderLength)
            .IsRequired();

        builder.Property(entity => entity.ProviderVersion).HasMaxLength(64);

        builder.Property(entity => entity.Flags)
            .HasMaxLength(CommunityPersistenceConstants.NameLength)
            .IsRequired();

        builder.OwnsOne(entity => entity.Content, content =>
        {
            content.Property(value => value.Type)
                .HasColumnName("content_type")
                .HasConversion<string>()
                .HasMaxLength(CommunityPersistenceConstants.EnumLength)
                .IsRequired();

            content.Property(value => value.Id).HasColumnName("content_id").IsRequired();

            content.HasIndex(value => new { value.Type, value.Id })
                .HasDatabaseName("ix_content_reviews_content");

            content.Ignore(value => value.Key);
        });

        builder.Navigation(entity => entity.Content).IsRequired();
        builder.Ignore(entity => entity.DomainEvents);
        builder.Ignore(entity => entity.NeedsHumanReview);
        builder.Ignore(entity => entity.ParsedFlags);
    }
}
