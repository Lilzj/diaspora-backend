using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Conversations.Domain.ValueObjects;

/// <summary>
/// What a message is a reply to, captured at the moment of replying.
/// <para>
/// <b>The preview is a copy, and copying it is the point.</b> A reply bubble renders a line of the
/// message it answers. Reading that line live would mean a reply to "I can do £4,000" silently
/// becoming a reply to "[deleted]" the moment the other party withdrew it — or worse, becoming a
/// reply to whatever they edited it into, which would let somebody retroactively change what you
/// appear to have agreed to. Freezing the preview means the thread still reads correctly and the
/// original message is still marked deleted where it sits.
/// </para>
/// <para>
/// The sender's <em>name</em> is deliberately absent even though the client's <c>ReplyReference</c>
/// carries one: this module resolves no names. The client already has every participant's display
/// name on the conversation and resolves it from <see cref="SenderId"/>, which is the same answer
/// B13 reached for typing indicators and for the same reason.
/// </para>
/// </summary>
public sealed class MessageReply : ValueObject
{
    public const int MaxPreviewLength = 160;

    private MessageReply(
        ConversationMessageId messageId,
        UserId senderId,
        string previewText,
        AttachmentKind? attachmentKind)
    {
        MessageId = messageId;
        SenderId = senderId;
        PreviewText = previewText;
        AttachmentKind = attachmentKind;
    }

    private MessageReply() { }

    public ConversationMessageId MessageId { get; private set; } = default!;

    public UserId SenderId { get; private set; } = default!;

    /// <summary>A frozen excerpt. Empty when the original carried no text.</summary>
    public string PreviewText { get; private set; } = default!;

    /// <summary>What the original carried, when it carried a file rather than words.</summary>
    public AttachmentKind? AttachmentKind { get; private set; }

    public static MessageReply Create(
        ConversationMessageId messageId,
        UserId senderId,
        string? previewText,
        AttachmentKind? attachmentKind = null)
    {
        ArgumentNullException.ThrowIfNull(messageId);
        ArgumentNullException.ThrowIfNull(senderId);

        var preview = string.IsNullOrWhiteSpace(previewText) ? string.Empty : previewText.Trim();

        return new MessageReply(
            messageId,
            senderId,
            preview.Length <= MaxPreviewLength ? preview : preview[..MaxPreviewLength],
            attachmentKind);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return MessageId;
        yield return SenderId;
        yield return PreviewText;
        yield return AttachmentKind;
    }
}
