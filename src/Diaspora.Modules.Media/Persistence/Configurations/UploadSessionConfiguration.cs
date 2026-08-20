using Diaspora.Modules.Media.Domain.Identifiers;
using Diaspora.Modules.Media.Domain.Uploads;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Media.Persistence.Configurations;

/// <summary>
/// Maps the <see cref="UploadSession"/> aggregate and its received parts.
/// <para>
/// Parts are an owned collection: nothing queries a chunk across sessions, and cascading their
/// deletion with the session is what keeps an expiry sweep from having to remember them
/// separately.
/// </para>
/// </summary>
public sealed class UploadSessionConfiguration : IEntityTypeConfiguration<UploadSession>
{
    public void Configure(EntityTypeBuilder<UploadSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(MediaPersistenceConstants.UploadSessionsTable, MediaModuleMetadata.Schema);

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id)
            .HasConversion(MediaValueConverters.Id<UploadSessionId>());

        builder.Property(session => session.StartedBy)
            .HasConversion(MediaValueConverters.Id<UserId>())
            .IsRequired();

        builder.Property(session => session.Status)
            .HasConversion<string>()
            .HasMaxLength(MediaPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(session => session.Protocol)
            .HasConversion<string>()
            .HasMaxLength(MediaPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(session => session.Kind)
            .HasConversion<string>()
            .HasMaxLength(MediaPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(session => session.DeclaredContentType)
            .HasConversion(MediaValueConverters.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(MediaPersistenceConstants.ContentTypeLength)
            .IsRequired();

        builder.Property(session => session.FileName)
            .HasConversion(MediaValueConverters.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(MediaPersistenceConstants.FileNameLength)
            .IsRequired();

        builder.Property(session => session.DeclaredSize)
            .HasConversion(MediaValueConverters.ByteSize)
            .HasColumnName("declared_size_bytes")
            .IsRequired();

        builder.Property(session => session.DeclaredHash)
            .HasConversion(MediaValueConverters.ContentHash)
            .HasColumnName("declared_content_hash")
            .HasMaxLength(MediaPersistenceConstants.ContentHashLength);

        builder.Property(session => session.ChunkSize)
            .HasConversion(MediaValueConverters.ByteSize)
            .HasColumnName("chunk_size_bytes")
            .IsRequired();

        builder.Property(session => session.StorageProvider)
            .HasMaxLength(MediaPersistenceConstants.StorageProviderLength)
            .IsRequired();

        builder.Property(session => session.StorageContainer)
            .HasMaxLength(MediaPersistenceConstants.StorageContainerLength)
            .IsRequired();

        builder.Property(session => session.MediaId)
            .HasConversion(MediaValueConverters.Id<MediaId>());

        builder.Property(session => session.Role)
            .HasMaxLength(MediaPersistenceConstants.RoleLength);

        builder.Property(session => session.FailureReason)
            .HasMaxLength(MediaPersistenceConstants.DetailLength);

        builder.OwnsOne(session => session.Owner, owner =>
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

        ConfigureChunks(builder);

        // The quota check and the "what was I uploading" screen, in one index.
        builder.HasIndex(session => new { session.StartedBy, session.Status })
            .HasDatabaseName("ix_upload_sessions_user_status");

        // The expiry sweep. Without it, a scan of every session ever created, on a timer.
        builder.HasIndex(session => new { session.Status, session.ExpiresAt })
            .HasDatabaseName("ix_upload_sessions_status_expiry");

        builder.Ignore(session => session.DomainEvents);
        builder.Ignore(session => session.ReceivedChunkNumbers);
        builder.Ignore(session => session.ReceivedSize);
        builder.Ignore(session => session.ProgressPercent);
        builder.Ignore(session => session.IsComplete);
        builder.Ignore(session => session.IsOpen);
    }

    private static void ConfigureChunks(EntityTypeBuilder<UploadSession> builder) =>
        builder.OwnsMany(session => session.Chunks, chunk =>
        {
            chunk.ToTable(MediaPersistenceConstants.UploadChunksTable, MediaModuleMetadata.Schema);

            chunk.HasKey(value => value.Id);

            chunk.Property(value => value.Id)
                .HasConversion(MediaValueConverters.Id<UploadChunkId>());

            chunk.Property(value => value.SessionId)
                .HasConversion(MediaValueConverters.Id<UploadSessionId>())
                .IsRequired();

            chunk.Property(value => value.Size)
                .HasConversion(MediaValueConverters.ByteSize)
                .HasColumnName("size_bytes")
                .IsRequired();

            chunk.Property(value => value.Hash)
                .HasConversion(MediaValueConverters.ContentHash)
                .HasColumnName("content_hash")
                .HasMaxLength(MediaPersistenceConstants.ContentHashLength)
                .IsRequired();

            chunk.Property(value => value.Key)
                .HasConversion(MediaValueConverters.StorageKey)
                .HasColumnName("storage_key")
                .HasMaxLength(MediaPersistenceConstants.StorageKeyLength)
                .IsRequired();

            chunk.WithOwner().HasForeignKey(value => value.SessionId);

            // The idempotency rule made physical: the same part number cannot be stored twice
            // for one session, whatever two concurrent requests believe.
            chunk.HasIndex(value => new { value.SessionId, value.Number })
                .IsUnique()
                .HasDatabaseName("ix_upload_chunks_session_number");

            chunk.Ignore(value => value.DomainEvents);
        });
}
