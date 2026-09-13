using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Errors;
using Diaspora.Modules.Conversations.Domain.Events;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.Policies;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Conversations.Domain.Messages;

/// <summary>
/// One thing somebody said.
/// <para>
/// <b>Its own aggregate, not a collection on the conversation.</b> Messages are the one unbounded
/// thing in this module — an enquiry that runs for a month holds thousands — and an owned collection
/// would load every one of them to append the next. Every read here is a cursor page and every write
/// is a single insert.
/// </para>
/// <para>
/// <b>Deletion is a tombstone, not a row removal, and the distinction is load-bearing in two
/// directions.</b> The reply that quoted it must still render; the cursor page that returns twenty
/// must still return twenty. So <see cref="Delete"/> clears the body, clears the attachment and sets
/// <see cref="MessageStatus.Deleted"/>, and the row stays. The platform's soft-delete filter is
/// deliberately <em>not</em> what does this: a global query filter would hide the row from the very
/// query that has to return it as a tombstone. <see cref="ISoftDeletable"/> is implemented for
/// retention — an operator purging a closed conversation — and is a different event with a different
/// meaning.
/// </para>
/// </summary>
public sealed class ConversationMessage
    : AggregateRoot<ConversationMessageId>, IAuditableEntity, ISoftDeletable
{
    private ConversationMessage(
        ConversationMessageId id,
        ConversationId conversationId,
        UserId? senderId,
        MessageKind kind,
        string? body,
        AttachmentReference? attachment,
        MessageReply? replyTo,
        DateTimeOffset sentAt)
        : base(id)
    {
        ConversationId = conversationId;
        SenderId = senderId;
        Kind = kind;
        Body = body;
        Attachment = attachment;
        ReplyTo = replyTo;
        Status = MessageStatus.Sent;
        SentAt = sentAt;
    }

    private ConversationMessage() { }

    public ConversationId ConversationId { get; private set; } = default!;

    /// <summary>
    /// Who said it, or null for a system message.
    /// <para>
    /// Nullable rather than a sentinel identifier. A system message attributed to
    /// <c>Guid.Empty</c> is a message attributed to a member whose row does not exist, and every
    /// consumer would have to know the magic value — including the notification path, which would
    /// otherwise try to look up a display name for nobody.
    /// </para>
    /// </summary>
    public UserId? SenderId { get; private set; }

    public MessageKind Kind { get; private set; }

    public MessageStatus Status { get; private set; }

    /// <summary>The words. Null once deleted, and null on a message that only carried a file.</summary>
    public string? Body { get; private set; }

    public AttachmentReference? Attachment { get; private set; }

    public MessageReply? ReplyTo { get; private set; }

    /// <summary>Which system event produced this, when nobody sent it.</summary>
    public SystemMessageKind? SystemKind { get; private set; }

    /// <summary>
    /// The client-supplied optimistic identifier, echoed back.
    /// <para>
    /// The client renders a bubble the instant somebody presses send and needs to know which
    /// confirmed message replaces it. Stored rather than merely echoed so a client that retries a
    /// send after a timeout can be answered idempotently rather than posting twice.
    /// </para>
    /// </summary>
    public string? ClientMessageId { get; private set; }

    public bool IsEdited { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    public DateTimeOffset SentAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    public DateTimeOffset? ReadAt { get; private set; }

    public DateTimeOffset? WithdrawnAt { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    /// <summary>Withdrawn by its sender. Not the same as purged by retention.</summary>
    public bool IsWithdrawn => Status == MessageStatus.Deleted;

    public bool IsSystem => Kind == MessageKind.System || SenderId is null;

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Appends a message.
    /// <para>
    /// <paramref name="id"/> is supplied rather than generated, so the caller can hand in a
    /// sequential identifier. Messages are the highest-volume insert in this module and a random key
    /// fragments the clustered index they are all written to; the platform's key-generation
    /// hardening exists for exactly this shape of table.
    /// </para>
    /// </summary>
    public static Result<ConversationMessage> Send(
        ConversationMessageId id,
        ConversationId conversationId,
        UserId senderId,
        string? body,
        DateTimeOffset now,
        MessageKind? kind = null,
        AttachmentReference? attachment = null,
        MessageReply? replyTo = null,
        string? clientMessageId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(conversationId);
        ArgumentNullException.ThrowIfNull(senderId);

        var text = Normalise(body);

        if (text is null && attachment is null)
        {
            return Result.Failure<ConversationMessage>(ConversationErrors.EmptyMessage);
        }

        // The kind follows the attachment when the caller named none, because a client that sends a
        // photograph and forgets to say so should still get an image bubble. A caller that names one
        // is believed: a document sent as a "text" message is somebody's deliberate choice and not
        // this module's business.
        var resolved = kind ?? attachment?.ImpliedMessageKind() ?? MessageKind.Text;

        if (resolved == MessageKind.System)
        {
            // A member cannot forge the platform's own voice. The only way to a system message is
            // System(), which takes no sender.
            resolved = MessageKind.Text;
        }

        var message = new ConversationMessage(
            id,
            conversationId,
            senderId,
            resolved,
            text,
            attachment,
            replyTo,
            now)
        {
            ClientMessageId = Clamp(clientMessageId, 64)
        };

        message.RaiseDomainEvent(new MessageSentDomainEvent
        {
            ConversationId = conversationId,
            MessageId = message.Id,
            SenderId = senderId,
            Kind = resolved,

            // Filled in by the conversation, which is the only thing that knows who is in it. The
            // aggregate raising this event cannot see the participant list, and a message that
            // guessed would guess wrong the first time somebody was added.
            RecipientUserIds = [],
            SentAt = now,
            ReplyToMessageId = replyTo?.MessageId,
            ReplyToSenderId = replyTo?.SenderId,
            HasAttachment = attachment is not null
        });

        return Result.Success(message);
    }

    /// <summary>
    /// A message the platform wrote.
    /// <para>
    /// Takes no sender and raises no <see cref="MessageSentDomainEvent"/>. A system message must not
    /// push: "this listing has been sold" arriving as a phone notification at 2am, for a
    /// conversation the member archived last month, is the notification that teaches people to
    /// switch notifications off. It appears in the thread when they next open it.
    /// </para>
    /// </summary>
    public static ConversationMessage System(
        ConversationMessageId id,
        ConversationId conversationId,
        SystemMessageKind systemKind,
        string body,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(conversationId);

        return new ConversationMessage(
            id,
            conversationId,
            senderId: null,
            MessageKind.System,
            Normalise(body) ?? systemKind.ToString(),
            attachment: null,
            replyTo: null,
            now)
        {
            SystemKind = systemKind
        };
    }

    // ════════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════════

    public Result Edit(UserId editor, string body, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(editor);

        if (IsSystem)
        {
            return Result.Failure(ConversationErrors.SystemMessageImmutable);
        }

        if (SenderId != editor)
        {
            return Result.Failure(ConversationErrors.NotTheSender);
        }

        if (IsWithdrawn)
        {
            return Result.Failure(ConversationErrors.MessageDeleted);
        }

        // Only text. Editing an image message would mean editing which file was sent, which is
        // sending a different message — and the recipient may already have opened the first one.
        if (Kind != MessageKind.Text)
        {
            return Result.Failure(ConversationErrors.MessageNotEditable);
        }

        if (now - SentAt > ConversationLimits.EditWindow)
        {
            return Result.Failure(ConversationErrors.EditWindowExpired);
        }

        var text = Normalise(body);

        if (text is null)
        {
            return Result.Failure(ConversationErrors.EmptyMessage);
        }

        Body = text;
        IsEdited = true;
        EditedAt = now;
        Revision++;

        RaiseDomainEvent(new MessageEditedDomainEvent
        {
            ConversationId = ConversationId,
            MessageId = Id,
            SenderId = editor,
            EditedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Withdraws a message. The row remains; nothing it said does.
    /// </summary>
    public Result Delete(UserId actor, DateTimeOffset now, bool asModerator = false)
    {
        ArgumentNullException.ThrowIfNull(actor);

        if (IsSystem)
        {
            return Result.Failure(ConversationErrors.SystemMessageImmutable);
        }

        if (!asModerator && SenderId != actor)
        {
            return Result.Failure(ConversationErrors.NotTheSender);
        }

        if (IsWithdrawn)
        {
            return Result.Success();
        }

        Status = asModerator ? MessageStatus.Hidden : MessageStatus.Deleted;

        // The body and the file both go. Keeping either would mean a member who deleted something
        // regrettable has deleted only the rendering of it, and the platform would still be holding
        // it the next time anybody exported the table.
        Body = null;
        Attachment = null;
        WithdrawnAt = now;
        Revision++;

        RaiseDomainEvent(new MessageDeletedDomainEvent
        {
            ConversationId = ConversationId,
            MessageId = Id,
            SenderId = SenderId ?? actor,
            DeletedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Records that a recipient's device has it.
    /// <para>
    /// Latches: once delivered, a later acknowledgement from a second device does not move the
    /// timestamp. The first device to receive it is when it was delivered, and a member with three
    /// devices should not see the tick time drift as each one wakes up.
    /// </para>
    /// </summary>
    public void MarkDelivered(DateTimeOffset now)
    {
        if (Status is MessageStatus.Deleted or MessageStatus.Hidden || DeliveredAt is not null)
        {
            return;
        }

        DeliveredAt = now;

        if (Status == MessageStatus.Sent)
        {
            Status = MessageStatus.Delivered;
        }

        Revision++;
    }

    /// <summary>Records that a recipient opened it. Latches, for the same reason delivery does.</summary>
    public void MarkRead(DateTimeOffset now)
    {
        if (Status is MessageStatus.Deleted or MessageStatus.Hidden || ReadAt is not null)
        {
            return;
        }

        ReadAt = now;
        DeliveredAt ??= now;
        Status = MessageStatus.Read;
        Revision++;
    }

    /// <summary>
    /// Attaches what only the conversation knows to the sent event: who receives the message, and
    /// whether it is the first message anybody has written there.
    /// <para>
    /// Called by the conversation immediately after <see cref="Send"/>, because the message cannot
    /// see the participant list and the handler that pushes must not have to reload the conversation
    /// to find out who to push to.
    /// </para>
    /// </summary>
    /// <param name="recipients">Everybody in the conversation except the sender.</param>
    /// <param name="opensConversation">
    /// True when the conversation had no message before this one — an enquiry becoming a real one
    /// rather than a chat somebody opened and left.
    /// </param>
    public void DeclareRecipients(IReadOnlyList<UserId> recipients, bool opensConversation)
    {
        ArgumentNullException.ThrowIfNull(recipients);

        var sent = DomainEvents.OfType<MessageSentDomainEvent>().FirstOrDefault();

        if (sent is null)
        {
            return;
        }

        RemoveDomainEvent(sent);
        AddDomainEvent(sent with { RecipientUserIds = recipients, OpensConversation = opensConversation });
    }

    private static string? Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= ConversationLimits.MaxMessageLength
            ? trimmed
            : trimmed[..ConversationLimits.MaxMessageLength];
    }

    private static string? Clamp(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }
}
