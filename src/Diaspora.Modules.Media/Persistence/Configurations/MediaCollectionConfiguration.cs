using Diaspora.Modules.Media.Domain.Collections;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Media.Persistence.Configurations;

/// <summary>Maps the <see cref="MediaCollection"/> aggregate and its placements.</summary>
public sealed class MediaCollectionConfiguration : IEntityTypeConfiguration<MediaCollection>
{
    public void Configure(EntityTypeBuilder<MediaCollection> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(MediaPersistenceConstants.CollectionsTable, MediaModuleMetadata.Schema);

        builder.HasKey(collection => collection.Id);

        builder.Property(collection => collection.Id)
            .HasConversion(MediaValueConverters.Id<MediaCollectionId>());

        builder.Property(collection => collection.Kind)
            .HasConversion<string>()
            .HasMaxLength(MediaPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(collection => collection.CreatedByUser)
            .HasConversion(MediaValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(collection => collection.Name)
            .HasMaxLength(MediaPersistenceConstants.CollectionNameLength);

        builder.Property(collection => collection.OwnerKey)
            .HasMaxLength(96)
            .IsRequired();

        builder.OwnsOne(collection => collection.Owner, owner =>
        {
            owner.Property(value => value.Type)
                .HasConversion<string>()
                .HasColumnName("owner_type")
                .HasMaxLength(MediaPersistenceConstants.EnumLength)
                .IsRequired();

            owner.Property(value => value.Id)
                .HasColumnName("owner_id")
                .IsRequired();
        });

        builder.OwnsMany(collection => collection.Items, item =>
        {
            item.ToTable(
                MediaPersistenceConstants.CollectionItemsTable, MediaModuleMetadata.Schema);

            item.HasKey(value => value.Id);

            item.Property(value => value.Id)
                .HasConversion(MediaValueConverters.Id<MediaCollectionItemId>());

            item.Property(value => value.CollectionId)
                .HasConversion(MediaValueConverters.Id<MediaCollectionId>())
                .IsRequired();

            item.Property(value => value.MediaId)
                .HasConversion(MediaValueConverters.Id<MediaId>())
                .IsRequired();

            item.Property(value => value.Caption)
                .HasMaxLength(MediaPersistenceConstants.DetailLength);

            item.WithOwner().HasForeignKey(value => value.CollectionId);

            // One placement per asset per collection. Adding the same photograph twice is a
            // no-op in the aggregate; this is what makes it impossible under a race.
            item.HasIndex(value => new { value.CollectionId, value.MediaId })
                .IsUnique()
                .HasDatabaseName("ix_media_collection_items_collection_media");

            item.Ignore(value => value.DomainEvents);
        });

        // One collection per owner and kind. This is what makes "get the gallery of listing
        // 7f3c…" a well-defined question, and what makes creation idempotent under a retry
        // rather than merely usually-idempotent.
        builder.HasIndex(collection => collection.OwnerKey)
            .IsUnique()
            .HasDatabaseName("ix_media_collections_owner_kind");

        builder.Ignore(collection => collection.DomainEvents);
        builder.Ignore(collection => collection.OrderedItems);
        builder.Ignore(collection => collection.Primary);
        builder.Ignore(collection => collection.Capacity);
        builder.Ignore(collection => collection.IsFull);
    }
}
