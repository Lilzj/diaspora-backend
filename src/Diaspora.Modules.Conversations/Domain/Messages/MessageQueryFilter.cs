using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Policies;

namespace Diaspora.Modules.Conversations.Domain.Messages;

/// <summary>What a history query is asking for.</summary>
public sealed record MessageQueryFilter
{
    public required ConversationId ConversationId { get; init; }

    /// <summary>
    /// Whether withdrawn messages come back as tombstones.
    /// <para>
    /// True by default, and that is the correct default rather than the polite one. A client renders
    /// "This message was deleted" in place and needs the row to do it; excluding them would leave a
    /// reply quoting a message that is not in the page, and the reply would render an empty bubble.
    /// </para>
    /// </summary>
    public bool IncludeWithdrawn { get; init; } = true;

    /// <summary>Restrict to messages carrying a file. Drives the media gallery.</summary>
    public bool AttachmentsOnly { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = ConversationLimits.DefaultPageSize;

    public int EffectiveLimit => Limit switch
    {
        < 1 => ConversationLimits.DefaultPageSize,
        > ConversationLimits.MaxPageSize => ConversationLimits.MaxPageSize,
        _ => Limit
    };
}
