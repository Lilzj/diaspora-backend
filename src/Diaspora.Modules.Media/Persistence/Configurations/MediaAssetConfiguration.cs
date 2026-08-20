using Diaspora.Modules.Media.Domain.Assets;
using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Media.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="MediaAsset"/> aggregate, its variants and its grants.
/// <para>
/// Variants and grants are owned collections rather than their own tables with their own
/// aggregates, because neither has a life outside the asset: nothing queries a variant across
/// assets, nothing references a grant by identifier, and both are always loaded and saved with
/// the asset. Owning them makes the cascade delete free and keeps "one variant per purpose"
/// enforceable in one place.
/// </para>
/// <para>
/// The indexes are chosen from the four questions the read paths actually ask: give me an
/// asset (the key), give me everything attached to this listing (owner), give me this member's
/// library (uploader), and does anything already have these exact bytes (hash). The last is
/// the one that would otherwise be a full scan, and it is on the upload path.
/// </para>
/// </summary>
public sealed class MediaAssetConfiguration : IEntityTypeConfiguration<MediaAsset>
{
    public void Configure(EntityTypeBuilder<MediaAsset> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(MediaPersistenceConstants.AssetsTable, MediaModuleMetadata.Schema);

        builder.HasKey(asset => asset.Id);

        builder.Property(asset => asset.Id)
            .HasConversion(MediaValueConverters.Id<MediaId>());

        builder.Property(asset => asset.UploadedBy)
            .HasConversion(MediaValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(asset => asset.Kind)
            .HasConversion<string>()
            .HasMaxLength(MediaPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(asset => asset.Status)
            .HasConversion<string>()
            .HasMaxLength(MediaPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(asset => asset.Visibility)
            .HasConversion<string>()
            .HasMaxLength(MediaPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(asset => asset.ContentType)
            .HasConversion(MediaValueConverters.ContentType)
            .HasMaxLength(MediaPersistenceConstants.ContentTypeLength)
            .IsRequired();

        // Stores the member-facing original; the sanitised form is derived on materialisation.
        // Sanitisation is deterministic and lossy, so this is the only direction that
        // round-trips.
        builder.Property(asset => asset.FileName)
            .HasConversion(MediaValueConverters.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(MediaPersistenceConstants.FileNameLength)
            .IsRequired();

        builder.Property(asset => asset.Size)
            .HasConversion(MediaValueConverters.ByteSize)
            .HasColumnName("size_bytes")
            .IsRequired();

        builder.Property(asset => asset.Hash)
            .HasConversion(MediaValueConverters.ContentHash)
            .HasColumnName("content_hash")
            .HasMaxLength(MediaPersistenceConstants.ContentHashLength)
            .IsRequired();

        builder.Property(asset => asset.Role)
            .HasMaxLength(MediaPersistenceConstants.RoleLength);

        builder.Property(asset => asset.AltText)
            .HasMaxLength(MediaPersistenceConstants.AltTextLength);

        builder.Property(asset => asset.Caption)
            .HasMaxLength(MediaPersistenceConstants.CaptionLength);

        builder.Property(asset => asset.FailureReason)
            .HasMaxLength(MediaPersistenceConstants.CaptionLength);

        ConfigureLocation(builder);
        ConfigureOwner(builder);
        ConfigureMetadata(builder);
        ConfigureScanResult(builder);
        ConfigureVariants(builder);
        ConfigureGrants(builder);

        builder.Property(asset => asset.CreatedAt).IsRequired();
        builder.Property(asset => asset.IsDeleted).IsRequired();

        builder.HasIndex(asset => asset.UploadedBy)
            .HasDatabaseName("ix_media_assets_uploaded_by");

        // Deduplication on write and reuse detection on read. Non-unique: two members
        // uploading the same photograph is normal, and refusing the second would be wrong.
        builder.HasIndex(asset => asset.Hash)
            .HasDatabaseName("ix_media_assets_hash");

        // The orphan sweep. Without this index it is a full scan on a timer.
        builder.HasIndex(asset => new { asset.Status, asset.CreatedAt })
            .HasDatabaseName("ix_media_assets_status_created");

        builder.Ignore(asset => asset.DomainEvents);
        builder.Ignore(asset => asset.RequiresSignedAccess);
        builder.Ignore(asset => asset.SignedUrlLifetime);
    }

    private static void ConfigureLocation(EntityTypeBuilder<MediaAsset> builder) =>
        builder.OwnsOne(asset => asset.Location, location =>
        {
            location.Property(value => value.Provider)
                .HasColumnName("storage_provider")
                .HasMaxLength(MediaPersistenceConstants.StorageProviderLength)
                .IsRequired();

            location.Property(value => value.Container)
                .HasColumnName("storage_container")
                .HasMaxLength(MediaPersistenceConstants.StorageContainerLength)
                .IsRequired();

            location.Property(value => value.Key)
                .HasConversion(MediaValueConverters.StorageKey)
                .HasColumnName("storage_key")
                .HasMaxLength(MediaPersistenceConstants.StorageKeyLength)
                .IsRequired();

            location.Property(value => value.Tier)
                .HasConversion<string>()
                .HasColumnName("storage_tier")
                .HasMaxLength(MediaPersistenceConstants.EnumLength)
                .IsRequired();
        });

    private static void ConfigureOwner(EntityTypeBuilder<MediaAsset> builder) =>
        builder.OwnsOne(asset => asset.Owner, owner =>
        {
            owner.Property(value => value.Type)
                .HasConversion<string>()
                .HasColumnName("owner_type")
                .HasMaxLength(MediaPersistenceConstants.EnumLength)
                .IsRequired();

            owner.Property(value => value.Id)
                .HasColumnName("owner_id")
                .IsRequired();

            // Everything attached to one listing, in one query. Declared on the owned builder
            // rather than the aggregate's because the columns belong to the owned type even
            // though they share the asset's table.
            owner.HasIndex(value => new { value.Type, value.Id })
                .HasDatabaseName("ix_media_assets_owner");
        });

    private static void ConfigureMetadata(EntityTypeBuilder<MediaAsset> builder) =>
        builder.OwnsOne(asset => asset.Metadata, metadata =>
        {
            metadata.OwnsOne(value => value.Dimensions, dimensions =>
            {
                dimensions.Property(value => value.Width).HasColumnName("width");
                dimensions.Property(value => value.Height).HasColumnName("height");
            });

            metadata.Property(value => value.Duration).HasColumnName("duration");
            metadata.Property(value => value.PageCount).HasColumnName("page_count");
            metadata.Property(value => value.Orientation).HasColumnName("orientation");

            metadata.Property(value => value.HasEmbeddedMetadata)
                .HasColumnName("has_embedded_metadata")
                .IsRequired();

            metadata.Property(value => value.BlurHash)
                .HasColumnName("blur_hash")
                .HasMaxLength(128);
        });

    private static void ConfigureScanResult(EntityTypeBuilder<MediaAsset> builder) =>
        builder.OwnsOne(asset => asset.ScanResult, scan =>
        {
            scan.Property(value => value.Status)
                .HasConversion<string>()
                .HasColumnName("scan_status")
                .HasMaxLength(MediaPersistenceConstants.EnumLength)
                .IsRequired();

            scan.Property(value => value.Scanner)
                .HasColumnName("scanner")
                .HasMaxLength(MediaPersistenceConstants.ScannerLength);

            scan.Property(value => value.ThreatName)
                .HasColumnName("threat_name")
                .HasMaxLength(MediaPersistenceConstants.ThreatLength);

            scan.Property(value => value.ScannedAt).HasColumnName("scanned_at");

            scan.Ignore(value => value.IsSafeToServe);
            scan.Ignore(value => value.RequiresQuarantine);
        });

    private static void ConfigureVariants(EntityTypeBuilder<MediaAsset> builder) =>
        builder.OwnsMany(asset => asset.Variants, variant =>
        {
            variant.ToTable(MediaPersistenceConstants.VariantsTable, MediaModuleMetadata.Schema);

            variant.HasKey(value => value.Id);

            variant.Property(value => value.Id)
                .HasConversion(MediaValueConverters.Id<MediaVariantId>());

            variant.Property(value => value.MediaId)
                .HasConversion(MediaValueConverters.Id<MediaId>())
                .IsRequired();

            variant.Property(value => value.Purpose)
                .HasConversion<string>()
                .HasMaxLength(MediaPersistenceConstants.EnumLength)
                .IsRequired();

            variant.Property(value => value.ContentType)
                .HasConversion(MediaValueConverters.ContentType)
                .HasMaxLength(MediaPersistenceConstants.ContentTypeLength)
                .IsRequired();

            variant.Property(value => value.Size)
                .HasConversion(MediaValueConverters.ByteSize)
                .HasColumnName("size_bytes")
                .IsRequired();

            variant.Property(value => value.TransformationKey)
                .HasMaxLength(MediaPersistenceConstants.TransformationKeyLength);

            variant.OwnsOne(value => value.Location, location =>
            {
                location.Property(inner => inner.Provider)
                    .HasColumnName("storage_provider")
                    .HasMaxLength(MediaPersistenceConstants.StorageProviderLength)
                    .IsRequired();

                location.Property(inner => inner.Container)
                    .HasColumnName("storage_container")
                    .HasMaxLength(MediaPersistenceConstants.StorageContainerLength)
                    .IsRequired();

                location.Property(inner => inner.Key)
                    .HasConversion(MediaValueConverters.StorageKey)
                    .HasColumnName("storage_key")
                    .HasMaxLength(MediaPersistenceConstants.StorageKeyLength)
                    .IsRequired();

                location.Property(inner => inner.Tier)
                    .HasConversion<string>()
                    .HasColumnName("storage_tier")
                    .HasMaxLength(MediaPersistenceConstants.EnumLength)
                    .IsRequired();
            });

            variant.OwnsOne(value => value.Dimensions, dimensions =>
            {
                dimensions.Property(inner => inner.Width).HasColumnName("width");
                dimensions.Property(inner => inner.Height).HasColumnName("height");
            });

            variant.WithOwner().HasForeignKey(value => value.MediaId);

            // The aggregate's "one variant per purpose" rule, made physical. Two rows would
            // mean the read path picks by row order.
            variant.HasIndex(value => new { value.MediaId, value.Purpose })
                .IsUnique()
                .HasDatabaseName("ix_media_variants_media_purpose");

            variant.Ignore(value => value.DomainEvents);
        });

    private static void ConfigureGrants(EntityTypeBuilder<MediaAsset> builder) =>
        builder.OwnsMany(asset => asset.Grants, grant =>
        {
            grant.ToTable(MediaPersistenceConstants.GrantsTable, MediaModuleMetadata.Schema);

            grant.HasKey(value => value.Id);

            grant.Property(value => value.Id)
                .HasConversion(MediaValueConverters.Id<MediaGrantId>());

            grant.Property(value => value.MediaId)
                .HasConversion(MediaValueConverters.Id<MediaId>())
                .IsRequired();

            grant.Property(value => value.GranteeId)
                .HasConversion(MediaValueConverters.Id<UserId>())
                .IsRequired();

            grant.Property(value => value.GrantedBy)
                .HasConversion(MediaValueConverters.Id<UserId>())
                .IsRequired();

            grant.Property(value => value.Level)
                .HasConversion<string>()
                .HasMaxLength(MediaPersistenceConstants.EnumLength)
                .IsRequired();

            grant.WithOwner().HasForeignKey(value => value.MediaId);

            grant.HasIndex(value => new { value.MediaId, value.GranteeId })
                .HasDatabaseName("ix_media_grants_media_grantee");

            grant.Ignore(value => value.DomainEvents);
            grant.Ignore(value => value.IsRevoked);
        });
}
