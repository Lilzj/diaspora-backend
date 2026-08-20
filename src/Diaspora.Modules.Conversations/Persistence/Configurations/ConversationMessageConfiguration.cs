using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Messages;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Conversations.Persistence.Configurations;

/// <summary>The message table. The largest table this module owns, and the one every index serves.</summary>
public sealed class ConversationMessageConfiguration : IEntityTypeConfiguration<ConversationMessage>
{
    public void Configure(EntityTypeBuilder<ConversationMessage> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            ConversationPersistenceConstants.MessagesTable, ConversationsModuleMetadata.Schema);

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasConversion(ConversationValueConverters.Id<ConversationMessageId>())
            .ValueGeneratedNever();

        builder.Property(message => message.Revision).IsConcurrencyToken();

        builder.Property(message => message.ConversationId)
            .HasConversion(ConversationValueConverters.Id<ConversationId>())
            .IsRequired();

        // Nullable: a system message has no sender. See ConversationMessage.SenderId for why that is
        // a null rather than a sentinel.
        builder.Property(message => message.SenderId)
            .HasConversion(ConversationValueConverters.Id<UserId>());

        builder.Property(message => message.Kind)
            .HasConversion<string>()
            .HasMaxLength(ConversationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(message => message.Status)
            .HasConversion<string>()
            .HasMaxLength(ConversationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(message => message.SystemKind)
            .HasConversion<string>()
            .HasMaxLength(ConversationPersistenceConstants.EnumLength);

        builder.Property(message => message.Body)
            .HasMaxLength(ConversationLimits.MaxMessageLength);

        builder.Property(message => message.ClientMessageId)
            .HasMaxLength(ConversationPersistenceConstants.ClientIdLength);

        ConfigureAttachment(builder);
        ConfigureReply(builder);
        ConfigureIndexes(builder);

        builder.Ignore(message => message.DomainEvents);
        builder.Ignore(message => message.IsWithdrawn);
        builder.Ignore(message => message.IsSystem);
    }

    /// <summary>
    /// The attachment, flattened onto the message row.
    /// <para>
    /// Owned-one rather than a table, because a message carries at most one file and a join per
    /// message would be a join per row of every history page.
    /// </para>
    /// </summary>
    private static void ConfigureAttachment(EntityTypeBuilder<ConversationMessage> builder) =>
        builder.OwnsOne(message => message.Attachment, attachment =>
        {
            attachment.Property(value => value.AttachmentId)
                .HasColumnName("attachment_id")
                .HasConversion(ConversationValueConverters.Id<ConversationAttachmentId>());

            attachment.Property(value => value.Kind)
                .HasColumnName("attachment_kind")
                .HasConversion<string>()
                .HasMaxLength(ConversationPersistenceConstants.EnumLength);

            attachment.Property(value => value.MediaAssetId).HasColumnName("attachment_media_id");
            attachment.Property(value => value.ThumbnailMediaId).HasColumnName("attachment_thumbnail_media_id");

            attachment.Property(value => value.MimeType)
                .HasColumnName("attachment_mime_type")
                .HasMaxLength(AttachmentReference.MaxMimeTypeLength);

            attachment.Property(value => value.SizeBytes).HasColumnName("attachment_size_bytes");
            attachment.Property(value => value.Width).HasColumnName("attachment_width");
            attachment.Property(value => value.Height).HasColumnName("attachment_height");
            attachment.Property(value => value.DurationSeconds).HasColumnName("attachment_duration_seconds");
            attachment.Property(value => value.PageCount).HasColumnName("attachment_page_count");

            attachment.Property(value => value.FileName)
                .HasColumnName("attachment_file_name")
                .HasMaxLength(AttachmentReference.MaxFileNameLength);

            attachment.Property(value => value.Caption)
                .HasColumnName("attachment_caption")
                .HasMaxLength(AttachmentReference.MaxCaptionLength);
        });

    private static void ConfigureReply(EntityTypeBuilder<ConversationMessage> builder) =>
        builder.OwnsOne(message => message.ReplyTo, reply =>
        {
            reply.Property(value => value.MessageId)
                .HasColumnName("reply_to_message_id")
                .HasConversion(ConversationValueConverters.Id<ConversationMessageId>());

            reply.Property(value => value.SenderId)
                .HasColumnName("reply_to_sender_id")
                .HasConversion(ConversationValueConverters.Id<UserId>());

            reply.Property(value => value.PreviewText)
                .HasColumnName("reply_to_preview")
                .HasMaxLength(MessageReply.MaxPreviewLength);

            reply.Property(value => value.AttachmentKind)
                .HasColumnName("reply_to_attachment_kind")
                .HasConversion<string>()
                .HasMaxLength(ConversationPersistenceConstants.EnumLength);
        });

    private static void ConfigureIndexes(EntityTypeBuilder<ConversationMessage> builder)
    {
        // The history page, and the only index that has to be right. Descending by SentAt with the
        // identifier as the tiebreak is what makes the keyset cursor able to exclude by identity
        // rather than by timestamp — two messages sent in the same millisecond are ordinary, and a
        // timestamp-only cursor silently drops one of them.
        builder.HasIndex(message => new { message.ConversationId, message.SentAt, message.Id })
            .HasDatabaseName("ix_conversation_messages_history");

        // The idempotent resend check.
        builder.HasIndex(message => new { message.ConversationId, message.ClientMessageId })
            .HasDatabaseName("ix_conversation_messages_client_id");

        // Receipts: the sender's unacknowledged messages in one conversation.
        builder.HasIndex(message => new { message.ConversationId, message.SenderId, message.Status })
            .HasDatabaseName("ix_conversation_messages_receipts");

        // The media gallery gets no index of its own, and that is deliberate rather than an
        // omission. It filters on one conversation and orders by SentAt, which the history index
        // above already answers; the "has an attachment" test is a residual filter over a page that
        // is bounded to a hundred rows. An index over the owned attachment's column would also have
        // to be declared inside the OwnsOne builder — the string HasIndex overload takes property
        // names, not column names, and naming the column here produced a shadow-property failure at
        // migration time rather than a compile error.
    }
}
