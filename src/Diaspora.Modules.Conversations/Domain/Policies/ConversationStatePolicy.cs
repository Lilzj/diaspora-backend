using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Enums;

namespace Diaspora.Modules.Conversations.Domain.Policies;

/// <summary>
/// Collapses the aggregate's status and one viewer's participant row into the single field the
/// client renders.
/// <para>
/// <b>The whole of the storage-versus-wire reconciliation lives in this one function, on purpose.</b>
/// The client's <c>ConversationState</c> is one string; the truth behind it is a shared status plus
/// four per-participant flags plus a block list, and it differs per viewer. Computing it at three
/// mapping sites would guarantee three answers — the inbox saying Archived while the conversation
/// screen says Active is the exact bug this centralises away.
/// </para>
/// <para>
/// <b>The precedence is deliberate and is not alphabetical.</b> Blocked outranks everything: a
/// blocked conversation must render as blocked even if it is also archived, because the composer is
/// disabled on that signal and a client that saw "Archived" would offer a text box that silently
/// fails. Closed outranks archived for the same reason. Archived outranks muted because archiving is
/// the stronger filing decision and the inbox filters on it. Muted outranks active because the
/// client renders a bell icon from it.
/// </para>
/// </summary>
public static class ConversationStatePolicy
{
    /// <summary>
    /// What one viewer should be told the state is.
    /// </summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="participant">The viewer's row, or null when they are not in it.</param>
    /// <param name="isBlocked">Whether a block exists in either direction between the two.</param>
    /// <param name="now">Used to decide whether a timed mute is still running.</param>
    public static ConversationState Resolve(
        Conversation conversation,
        ConversationParticipant? participant,
        bool isBlocked,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (isBlocked)
        {
            return ConversationState.Blocked;
        }

        if (conversation.Status == ConversationStatus.Closed)
        {
            return ConversationState.Closed;
        }

        if (participant is null)
        {
            // Not a participant. Reached only by a moderator reading somebody else's thread; an
            // ordinary caller was already refused with "does not exist".
            return conversation.Status == ConversationStatus.PendingRequest
                ? ConversationState.PendingRequest
                : ConversationState.Active;
        }

        if (conversation.Status == ConversationStatus.PendingRequest)
        {
            // The initiator sees an accepted-looking thread; the recipient sees a request they have
            // to answer. Showing the initiator "PendingRequest" would render a conversation they
            // opened as one waiting on them.
            return participant.Role == ParticipantRole.Recipient
                ? ConversationState.PendingRequest
                : ConversationState.Accepted;
        }

        if (participant.IsArchived)
        {
            return ConversationState.Archived;
        }

        if (participant.IsMutedAt(now))
        {
            return ConversationState.Muted;
        }

        return ConversationState.Active;
    }

    /// <summary>
    /// Whether this viewer may send into the conversation right now.
    /// <para>
    /// Mirrors the client's own composer rule — it disables on <c>Blocked</c> and <c>Closed</c> —
    /// and adds the case the client cannot see: an initiator who has already sent their one opening
    /// message into a request nobody has accepted.
    /// </para>
    /// </summary>
    public static bool CanSend(
        Conversation conversation, ConversationParticipant? participant, bool isBlocked)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (isBlocked || conversation.Status == ConversationStatus.Closed)
        {
            return false;
        }

        if (participant is not { IsActive: true })
        {
            return false;
        }

        return conversation.Status != ConversationStatus.PendingRequest
            || participant.Role != ParticipantRole.Initiator
            || conversation.MessageCount == 0;
    }
}
