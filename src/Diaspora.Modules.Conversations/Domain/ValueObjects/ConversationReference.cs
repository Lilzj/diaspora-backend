using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.ValueObjects;

/// <summary>
/// The pair that identifies what a conversation is about, without the display copy.
/// <para>
/// <b>This is the deduplication key, and the reason it is a type rather than two arguments.</b>
/// "One conversation per (context type, entity, pair of people)" is the rule the client relies on —
/// its <c>openOrCreateConversation</c> is the single entry point every module uses precisely so a
/// buyer who taps "Message seller" twice does not end up with two threads. Two call sites composing
/// that key by hand is how the second one ends up ordering the fields differently.
/// </para>
/// </summary>
public sealed class ConversationReference : ValueObject
{
    private ConversationReference(ConversationType type, string entityId)
    {
        Type = type;
        EntityId = entityId;
    }

    private ConversationReference() { }

    public ConversationType Type { get; private set; } = default!;

    public string EntityId { get; private set; } = default!;

    public static ConversationReference Create(ConversationType type, string entityId)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(entityId);

        var trimmed = entityId.Trim();

        return new ConversationReference(
            type,
            trimmed.Length <= ConversationContext.MaxEntityIdLength
                ? trimmed
                : trimmed[..ConversationContext.MaxEntityIdLength]);
    }

    public override string ToString() => $"{Type.Key}:{EntityId}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return EntityId;
    }
}
