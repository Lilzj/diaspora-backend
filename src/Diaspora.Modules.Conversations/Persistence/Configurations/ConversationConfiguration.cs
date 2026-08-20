using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Diaspora.Modules.Conversations.Persistence.Configurations;

/// <summary>
/// The conversation table, its two owned value objects and its owned participant collection.
/// </summary>
public sealed class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable(
            ConversationPersistenceConstants.ConversationsTable, ConversationsModuleMetadata.Schema);

        builder.HasKey(conversation => conversation.Id);

        builder.Property(conversation => conversation.Id)
            .HasConversion(ConversationValueConverters.Id<ConversationId>())
            .ValueGeneratedNever();

        // Two people muting the same conversation from two devices is ordinary; losing one of the
        // mutes because the second write overwrote the first is not.
        builder.Property(conversation => conversation.Revision).IsConcurrencyToken();

        builder.Property(conversation => conversation.Status)
            .HasConversion<string>()
            .HasMaxLength(ConversationPersistenceConstants.EnumLength)
            .IsRequired();

        builder.Property(conversation => conversation.LastMessageId)
            .HasConversion(ConversationValueConverters.Id<ConversationMessageId>());

        builder.Property(conversation => conversation.LastMessageSenderId)
            .HasConversion(ConversationValueConverters.Id<UserId>());

        builder.Property(conversation => conversation.ClosedReason)
            .HasMaxLength(ConversationPersistenceConstants.ReasonLength);

        ConfigureContext(builder);
        ConfigureMetadata(builder);
        ConfigureParticipants(builder);
        ConfigureIndexes(builder);

        builder.Ignore(conversation => conversation.DomainEvents);
        builder.Ignore(conversation => conversation.ActiveParticipants);
        builder.Ignore(conversation => conversation.IsClosed);
        builder.Ignore(conversation => conversation.IsPendingRequest);
    }

    /// <summary>
    /// The frozen snapshot, flattened onto the conversation row.
    /// <para>
    /// Owned rather than a separate table because it is read on every single inbox line: a join for
    /// the title of the thing being discussed would double the cost of the platform's most-rendered
    /// query to save nothing.
    /// </para>
    /// </summary>
    private static void ConfigureContext(EntityTypeBuilder<Conversation> builder) =>
        builder.OwnsOne(conversation => conversation.Context, context =>
        {
            // The catalogue key, unwrapped from its value object. A nested owned type inside an
            // owned type is legal and is also how this platform previously acquired a shadow key it
            // did not want, so ConversationType projects one column and no more.
            context.Property(value => value.Type)
                .HasColumnName("context_type")
                .HasConversion(
                    type => type.Key,
                    key => ConversationType.From(key))
                .HasMaxLength(ConversationType.MaxLength)
                .IsRequired();

            context.Property(value => value.EntityId)
                .HasColumnName("context_entity_id")
                .HasMaxLength(ConversationContext.MaxEntityIdLength)
                .IsRequired();

            context.Property(value => value.EntityTitle)
                .HasColumnName("context_entity_title")
                .HasMaxLength(ConversationContext.MaxTitleLength)
                .IsRequired();

            context.Property(value => value.EntitySubtitle)
                .HasColumnName("context_entity_subtitle")
                .HasMaxLength(ConversationContext.MaxSubtitleLength);

            context.Property(value => value.EntityImageMediaId)
                .HasColumnName("context_entity_image_media_id");

            context.Property(value => value.EntityPrice)
                .HasColumnName("context_entity_price")
                .HasMaxLength(ConversationContext.MaxPriceLength);

            context.Property(value => value.EntityStatus)
                .HasColumnName("context_entity_status")
                .HasMaxLength(ConversationContext.MaxStatusLength);

            context.Property(value => value.EntityStatusLabel)
                .HasColumnName("context_entity_status_label")
                .HasMaxLength(ConversationContext.MaxStatusLength);

            context.Property(value => value.DeepLink)
                .HasColumnName("context_deep_link")
                .HasMaxLength(ConversationContext.MaxDeepLinkLength);

            context.Property(value => value.SnapshotAt).HasColumnName("context_snapshot_at");

            // Find-or-create reads this. Declared inside the owned builder because the columns
            // belong to the owned type: an index declared on the conversation builder cannot name
            // them, and EF's string overload there resolves property names rather than columns.
            context.HasIndex(value => new { value.Type, value.EntityId })
                .HasDatabaseName("ix_conversations_reference");
        });

    private static void ConfigureMetadata(EntityTypeBuilder<Conversation> builder) =>
        builder.OwnsOne(conversation => conversation.Metadata, metadata =>
        {
            metadata.Property(value => value.CampaignSource)
                .HasColumnName("campaign_source")
                .HasMaxLength(ConversationMetadata.MaxFieldLength);

            metadata.Property(value => value.ReferralCode)
                .HasColumnName("referral_code")
                .HasMaxLength(ConversationMetadata.MaxFieldLength);

            metadata.Property(value => value.AppVersion)
                .HasColumnName("app_version")
                .HasMaxLength(ConversationMetadata.MaxFieldLength);

            metadata.Ignore(value => value.IsEmpty);
        });

    /// <summary>
    /// Participants, in their own table, owned by the conversation.
    /// <para>
    /// <b>The key is declared and never generated.</b> Without <c>HasKey</c> EF invents a
    /// store-generated integer inside a composite primary key, and SQLite refuses the second insert;
    /// without <c>ValueGeneratedNever</c> EF decides a caller-supplied key means the row already
    /// exists and emits an UPDATE that affects nothing, which surfaces as a bogus concurrency
    /// conflict. Both are failures this platform has already paid for once.
    /// </para>
    /// </summary>
    private static void ConfigureParticipants(EntityTypeBuilder<Conversation> builder) =>
        builder.OwnsMany(conversation => conversation.Participants, participant =>
        {
            participant.ToTable(
                ConversationPersistenceConstants.ParticipantsTable,
                ConversationsModuleMetadata.Schema);

            participant.HasKey(value => value.Id);

            participant.Property(value => value.Id)
                .HasConversion(ConversationValueConverters.Id<ParticipantId>())
                .ValueGeneratedNever();

            participant.Property(value => value.UserId)
                .HasConversion(ConversationValueConverters.Id<UserId>())
                .IsRequired();

            participant.Property(value => value.Role)
                .HasConversion<string>()
                .HasMaxLength(ConversationPersistenceConstants.EnumLength)
                .IsRequired();

            participant.Property(value => value.LastReadMessageId)
                .HasConversion(ConversationValueConverters.Id<ConversationMessageId>());

            participant.Property(value => value.LastDeliveredMessageId)
                .HasConversion(ConversationValueConverters.Id<ConversationMessageId>());

            // Named by shadow property because the owner key EF creates is not a CLR property, so
            // the expression overload cannot reach it.
            participant.HasIndex("ConversationId", nameof(ConversationParticipant.UserId))
                .IsUnique()
                .HasDatabaseName("ux_conversation_participants_member");

            // The inbox query. Everything it filters and orders by is on this index, so a member's
            // inbox never touches the conversation table to decide which rows they can see.
            participant.HasIndex(value => new
                {
                    value.UserId,
                    value.IsArchived,
                    value.LeftAt
                })
                .HasDatabaseName("ix_conversation_participants_inbox");

            participant.HasIndex(value => new { value.UserId, value.IsPinned })
                .HasDatabaseName("ix_conversation_participants_pinned");

            participant.HasIndex(value => new { value.UserId, value.UnreadCount })
                .HasDatabaseName("ix_conversation_participants_unread");

            // B23.x Wave 12. The two markers that stop the attention sweep reporting one silence
            // twice. Indexed together with the unread count because the sweep filters on both, and a
            // scan of every participation row every hour is the one way this feature becomes
            // expensive.
            participant.HasIndex(value => new { value.UnreadCount, value.UnreadNoticedAt })
                .HasDatabaseName("ix_conversation_participants_unread_notice");

            participant.Ignore(value => value.DomainEvents);
            participant.Ignore(value => value.IsActive);
            participant.Ignore(value => value.Cursor);
        });

    private static void ConfigureIndexes(EntityTypeBuilder<Conversation> builder)
    {
        builder.HasIndex(conversation => conversation.LastActivityAt)
            .HasDatabaseName("ix_conversations_activity");

        builder.HasIndex(conversation => new { conversation.Status, conversation.LastActivityAt })
            .HasDatabaseName("ix_conversations_status_activity");
    }
}
