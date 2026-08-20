using Diaspora.Persistence;
using Diaspora.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Diaspora.Modules.Conversations.Persistence.Configurations;

/// <summary>Table names and column widths, declared once so two configurations cannot disagree.</summary>
internal static class ConversationPersistenceConstants
{
    public const string ConversationsTable = "conversations";
    public const string ParticipantsTable = "conversation_participants";
    public const string MessagesTable = "conversation_messages";
    public const string BlocksTable = "conversation_blocks";
    public const string ContextTypesTable = "conversation_context_types";

    /// <summary>Enum names are stored as text, so this is the longest member name plus room.</summary>
    public const int EnumLength = 32;

    public const int KeyLength = 64;
    public const int ClientIdLength = 64;
    public const int ReasonLength = 280;
}

internal static class ConversationValueConverters
{
    public static ValueConverter Id<TId>() where TId : StronglyTypedId =>
        new StronglyTypedIdValueConverter<TId>();
}
