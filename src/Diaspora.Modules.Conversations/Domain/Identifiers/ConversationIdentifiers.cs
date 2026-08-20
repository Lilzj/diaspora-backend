using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.Identifiers;

/// <summary>
/// A conversation.
/// <para>
/// The one identifier that crosses this module's boundary in both directions: it is what the client
/// puts in a URL, what <see cref="Notifications.Extensibility.Realtime.RealtimeGroups"/> composes a
/// group name from, and what Marketplace stores as its external handle. Everything else stays here.
/// </para>
/// </summary>
public sealed record ConversationId(Guid Value) : StronglyTypedId(Value)
{
    public static ConversationId New() => new(Guid.NewGuid());

    public static ConversationId From(Guid value) => new(value);
}

/// <summary>A message inside a conversation.</summary>
public sealed record ConversationMessageId(Guid Value) : StronglyTypedId(Value)
{
    public static ConversationMessageId New() => new(Guid.NewGuid());

    public static ConversationMessageId From(Guid value) => new(value);
}

/// <summary>
/// A participant row.
/// <para>
/// <b>Distinct from the member's <c>UserId</c>, and that is not ceremony.</b> A member who leaves a
/// conversation and is added back is a second participation with its own join date and its own read
/// cursor; identifying the row by the member would make the two indistinguishable and silently
/// resurrect a read position from before they left.
/// </para>
/// </summary>
public sealed record ParticipantId(Guid Value) : StronglyTypedId(Value)
{
    public static ParticipantId New() => new(Guid.NewGuid());

    public static ParticipantId From(Guid value) => new(value);
}

/// <summary>One member's decision to stop hearing from another.</summary>
public sealed record ConversationBlockId(Guid Value) : StronglyTypedId(Value)
{
    public static ConversationBlockId New() => new(Guid.NewGuid());

    public static ConversationBlockId From(Guid value) => new(value);
}

/// <summary>
/// An attachment on a message.
/// <para>
/// The client mints this before the upload finishes — it is the handle its optimistic UI renders a
/// progress bar against — so the server accepts a caller-supplied value rather than generating one.
/// </para>
/// </summary>
public sealed record ConversationAttachmentId(Guid Value) : StronglyTypedId(Value)
{
    public static ConversationAttachmentId New() => new(Guid.NewGuid());

    public static ConversationAttachmentId From(Guid value) => new(value);
}
