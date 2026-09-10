namespace Diaspora.Modules.Conversations;

/// <summary>
/// Every error this module can return, in one list.
/// <para>
/// <b>The absence of a "conversation not found" distinct from "you are not in it" is deliberate and
/// is the module's central privacy rule.</b> A caller who is not a participant is told the
/// conversation does not exist, in the same words and with the same status as a caller who invented
/// an identifier. Telling them apart would let anybody enumerate which conversations exist by
/// watching whether the answer changed — which, for a surface where the existence of a conversation
/// between two named people is itself the private fact, is the whole leak.
/// </para>
/// </summary>
public static class ConversationErrorCodes
{
    private const string Prefix = "conversations.";

    /// <summary>The conversation does not exist, or the caller may not see that it does.</summary>
    public const string ConversationNotFound = Prefix + "conversation_not_found";

    /// <summary>The message does not exist inside a conversation the caller can see.</summary>
    public const string MessageNotFound = Prefix + "message_not_found";

    /// <summary>The caller is authenticated but is not a participant.</summary>
    public const string NotAParticipant = Prefix + "not_a_participant";

    /// <summary>The caller tried to edit or delete a message somebody else sent.</summary>
    public const string NotTheSender = Prefix + "not_the_sender";

    /// <summary>One party has blocked the other.</summary>
    public const string Blocked = Prefix + "blocked";

    /// <summary>The conversation is closed and accepts no further messages.</summary>
    public const string Closed = Prefix + "closed";

    /// <summary>The recipient has not yet accepted the request.</summary>
    public const string RequestPending = Prefix + "request_pending";

    /// <summary>A conversation needs two distinct people.</summary>
    public const string SelfConversation = Prefix + "self_conversation";

    public const string RecipientNotFound = Prefix + "recipient_not_found";

    /// <summary>The pin limit is reached.</summary>
    public const string PinLimitReached = Prefix + "pin_limit_reached";

    /// <summary>The message body is empty and no attachment was supplied.</summary>
    public const string EmptyMessage = Prefix + "empty_message";

    /// <summary>The edit window has passed.</summary>
    public const string EditWindowExpired = Prefix + "edit_window_expired";

    /// <summary>A deleted message cannot be edited.</summary>
    public const string MessageDeleted = Prefix + "message_deleted";

    /// <summary>Only text messages can be edited.</summary>
    public const string MessageNotEditable = Prefix + "message_not_editable";

    /// <summary>The context type is not in the catalogue.</summary>
    public const string UnknownContextType = Prefix + "unknown_context_type";

    /// <summary>The reply target is not in this conversation.</summary>
    public const string ReplyTargetNotFound = Prefix + "reply_target_not_found";

    /// <summary>The request does not exist or has already been answered.</summary>
    public const string RequestNotFound = Prefix + "request_not_found";

    /// <summary>A member cannot block themselves.</summary>
    public const string SelfBlock = Prefix + "self_block";

    /// <summary>Attachments are switched off in this deployment.</summary>
    public const string AttachmentsUnavailable = Prefix + "attachments_unavailable";

    /// <summary>A system message cannot be edited, deleted or replied to by a member.</summary>
    public const string SystemMessageImmutable = Prefix + "system_message_immutable";

    /// <summary>The caller sent messages faster than the deployment permits.</summary>
    public const string RateLimited = Prefix + "rate_limited";

    /// <summary>The conversation already holds as many participants as the platform supports.</summary>
    public const string ParticipantLimitReached = Prefix + "participant_limit_reached";
}
