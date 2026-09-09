using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Conversations.Domain.Errors;

/// <summary>
/// Every failure this module returns, worded once.
/// <para>
/// <b><see cref="ConversationNotFound"/> is returned for four different situations and says so in
/// none of them.</b> The conversation does not exist; the conversation exists and the caller is not
/// in it; the conversation exists, the caller was in it and has been removed; the conversation is
/// soft-deleted. Distinguishing those in the response would let anybody probe identifiers until the
/// wording changed and learn that a conversation exists between two people — which, on a surface
/// where a diaspora member may be talking to an immigration adviser or a landlord, is the private
/// fact itself, not merely metadata about it.
/// </para>
/// <para>
/// The one exception is <see cref="NotAParticipant"/>, which exists for internal call paths and
/// tests. It must never reach an HTTP response for a read; the authorisation service maps it to
/// <see cref="ConversationNotFound"/> at the boundary, and an integration test asserts the status
/// code is 404 rather than 403.
/// </para>
/// </summary>
public static class ConversationErrors
{
    public static readonly Error ConversationNotFound = Error.NotFound(
        ConversationErrorCodes.ConversationNotFound,
        "That conversation does not exist.");

    public static readonly Error MessageNotFound = Error.NotFound(
        ConversationErrorCodes.MessageNotFound,
        "That message does not exist.");

    public static readonly Error NotAParticipant = Error.Forbidden(
        ConversationErrorCodes.NotAParticipant,
        "You are not a participant in this conversation.");

    public static readonly Error NotTheSender = Error.Forbidden(
        ConversationErrorCodes.NotTheSender,
        "Only the sender can change a message.");

    public static readonly Error Blocked = Error.Forbidden(
        ConversationErrorCodes.Blocked,
        "This conversation is blocked.");

    public static readonly Error Closed = Error.Conflict(
        ConversationErrorCodes.Closed,
        "This conversation is closed and cannot take new messages.");

    public static readonly Error RequestPending = Error.Conflict(
        ConversationErrorCodes.RequestPending,
        "This conversation is a pending request. The recipient has to accept it first.");

    /// <summary>
    /// The other party is not a person on this platform.
    /// <para>
    /// Nothing used to check. A conversation could be opened against any identifier at all — an
    /// organization's, a listing's, a typo's — and the platform stored it: a conversation whose
    /// counterparty was not a user, which nothing could deliver to, which the other side could
    /// never open, and which rendered as "Unknown" above a context card naming the subject.
    /// </para>
    /// <para>
    /// Deliberately not "that user does not exist", which would let somebody test identifiers for
    /// account existence one request at a time.
    /// </para>
    /// </summary>
    public static readonly Error RecipientNotFound = Error.Validation(
        ConversationErrorCodes.RecipientNotFound,
        "You cannot start a conversation with this recipient.");

    public static readonly Error SelfConversation = Error.Validation(
        ConversationErrorCodes.SelfConversation,
        "A conversation needs two different people.");

    public static readonly Error PinLimitReached = Error.Conflict(
        ConversationErrorCodes.PinLimitReached,
        "You can pin up to five conversations. Unpin one first.");

    public static readonly Error EmptyMessage = Error.Validation(
        ConversationErrorCodes.EmptyMessage,
        "A message needs either text or an attachment.");

    public static readonly Error EditWindowExpired = Error.Conflict(
        ConversationErrorCodes.EditWindowExpired,
        "This message is too old to edit.");

    public static readonly Error MessageDeleted = Error.Conflict(
        ConversationErrorCodes.MessageDeleted,
        "A deleted message cannot be changed.");

    public static readonly Error MessageNotEditable = Error.Conflict(
        ConversationErrorCodes.MessageNotEditable,
        "Only text messages can be edited.");

    public static readonly Error UnknownContextType = Error.Validation(
        ConversationErrorCodes.UnknownContextType,
        "That conversation type is not one this platform knows about.");

    public static readonly Error ReplyTargetNotFound = Error.Validation(
        ConversationErrorCodes.ReplyTargetNotFound,
        "The message being replied to is not in this conversation.");

    public static readonly Error RequestNotFound = Error.NotFound(
        ConversationErrorCodes.RequestNotFound,
        "That request does not exist, or it has already been answered.");

    public static readonly Error SelfBlock = Error.Validation(
        ConversationErrorCodes.SelfBlock,
        "You cannot block yourself.");

    public static readonly Error AttachmentsUnavailable = Error.ServiceUnavailable(
        ConversationErrorCodes.AttachmentsUnavailable,
        "Attachments are not available on this deployment.");

    public static readonly Error SystemMessageImmutable = Error.Conflict(
        ConversationErrorCodes.SystemMessageImmutable,
        "System messages cannot be changed.");

    public static readonly Error RateLimited = Error.RateLimited(
        ConversationErrorCodes.RateLimited,
        "You are sending messages too quickly. Wait a moment and try again.");

    public static readonly Error ParticipantLimitReached = Error.Conflict(
        ConversationErrorCodes.ParticipantLimitReached,
        "This conversation already has as many participants as it can hold.");
}
