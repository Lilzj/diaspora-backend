using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Conversations.Domain.Conversations;

/// <summary>
/// How conversations are loaded and stored.
/// <para>
/// <b>Every read that returns conversations takes a viewer.</b> There is no "get by id" that does
/// not — a repository method that could load a conversation without saying who is asking is a method
/// somebody eventually calls from a query handler, and the leak is silent because the row comes back
/// looking perfectly ordinary. <see cref="GetForParticipantAsync"/> is the only door for member
/// traffic; <see cref="GetAsync"/> exists for moderation and for the integration consumers, and
/// its callers are counted by an architecture test.
/// </para>
/// </summary>
public interface IConversationRepository
{
    /// <summary>
    /// Loads a conversation with its participants, without any access check.
    /// <para>
    /// For moderation and for event consumers acting on the platform's behalf. Member-facing code
    /// uses <see cref="GetForParticipantAsync"/>.
    /// </para>
    /// </summary>
    Task<Conversation?> GetAsync(ConversationId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads a conversation only when the caller is an active participant, and null otherwise.
    /// <para>
    /// Null covers both "no such conversation" and "not yours", because the caller must not be able
    /// to tell them apart — see <c>ConversationErrors</c>.
    /// </para>
    /// </summary>
    Task<Conversation?> GetForParticipantAsync(
        ConversationId id, UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The existing conversation for this exact (reference, pair of people), or null.
    /// <para>
    /// The find half of find-or-create. Excludes closed conversations: a listing that sold and was
    /// re-listed deserves a new thread rather than resurrecting one that ended.
    /// </para>
    /// </summary>
    Task<Conversation?> FindByReferenceAsync(
        ConversationReference reference,
        UserId initiatorUserId,
        UserId recipientUserId,
        CancellationToken cancellationToken = default);

    /// <summary>One page of a member's inbox, newest activity first, pinned first.</summary>
    Task<CursorPagedResult<Conversation>> ListForUserAsync(
        ConversationQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>How many conversations this member currently has pinned. Enforces the limit.</summary>
    Task<int> CountPinnedAsync(UserId userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The member's total unread, excluding muted and archived conversations.
    /// <para>
    /// One indexed aggregate rather than a count per row, because it is read on every inbox render
    /// and on every realtime badge update.
    /// </para>
    /// </summary>
    Task<int> CountUnreadAsync(UserId userId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// Every open conversation naming this external entity, whoever is in them.
    /// <para>
    /// The integration path: a listing sold, a job filled, an event cancelled. Bounded by design —
    /// a single entity has as many conversations as it had enquirers, not as many as the platform
    /// has members.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Conversation>> ListByReferenceAsync(
        ConversationReference reference, CancellationToken cancellationToken = default);

    /// <summary>Every active conversation shared by two members. Used by the presence authorizer.</summary>
    Task<bool> ShareConversationAsync(
        UserId first, UserId second, CancellationToken cancellationToken = default);

    /// <summary>
    /// Active conversations whose last message is older than a cutoff — B23.x Wave 12.
    /// <para>
    /// The attention sweep's work list. Bounded by <paramref name="limit"/> and ordered oldest first,
    /// so one pass is a fixed amount of work whatever the platform's size and the longest-quiet
    /// threads are reported first.
    /// </para>
    /// <para>
    /// <b>Loaded with participants, deliberately.</b> Whether a member is owed a notice is a fact
    /// about their side of the thread — muted, archived, left, already told — and answering it in the
    /// database would be a second copy of a rule the aggregate already states once.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<Conversation>> ListQuietAsync(
        DateTimeOffset quietBefore, int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Members whose unread has been sitting above a threshold — B23.x Wave 12.
    /// <para>
    /// Aggregated in the database, per member, across every conversation they are in: the trigger is
    /// "I have unread messages", so one member is one answer however many threads it is spread over.
    /// Muted, archived and left-behind sides are excluded, matching <see cref="CountUnreadAsync"/> —
    /// a reminder that counted a muted thread would make muting look broken.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ConversationUnreadBacklog>> ListUnreadBacklogAsync(
        int minimumUnread,
        DateTimeOffset unreadSince,
        DateTimeOffset notNotifiedSince,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the unread cooldown for these members — B23.x Wave 12.
    /// <para>
    /// <b>On every side they hold, not on one.</b> The backlog reported is a total across a member's
    /// inbox, so a cooldown recorded against a single thread would let the next sweep report the same
    /// total from a different one and the member would hear about it every hour.
    /// </para>
    /// <para>
    /// Does not save. The caller commits the marks and the outbox rows in one unit of work — a mark
    /// without its publication silences a notice nobody sent, and a publication without its mark
    /// sends the same one again.
    /// </para>
    /// </summary>
    Task MarkUnreadReportedAsync(
        IReadOnlyList<Guid> memberUserIds,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);

    void Add(Conversation conversation);

    void Update(Conversation conversation);
}

/// <summary>
/// One member's standing unread, summed across their conversations — B23.x Wave 12.
/// </summary>
/// <param name="MemberUserId">Who is behind.</param>
/// <param name="UnreadCount">How many messages, across every thread that counts.</param>
/// <param name="ConversationCount">How many threads they are spread over.</param>
/// <param name="OldestUnreadAt">
/// The oldest message that is still unread, as the last activity on the thread carrying it.
/// <para>
/// An approximation, and a deliberate one: the exact answer is the timestamp of the earliest unread
/// message, which needs a join into the message table per participant and turns a bounded sweep into
/// one query per member. What a reminder needs is "how long has this been sitting", and the thread's
/// own last activity answers that within the sweep's own interval.
/// </para>
/// </param>
public readonly record struct ConversationUnreadBacklog(
    Guid MemberUserId,
    int UnreadCount,
    int ConversationCount,
    DateTimeOffset OldestUnreadAt);
