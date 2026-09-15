using Diaspora.Modules.Conversations.Domain.Conversations;
using Diaspora.Modules.Conversations.Domain.Enums;
using Diaspora.Modules.Conversations.Domain.Identifiers;
using Diaspora.Modules.Conversations.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Conversations.Persistence.Repositories;

/// <summary>
/// Conversations, read through their participants.
/// <para>
/// <b>Every member-facing query starts from the participant collection, not from the conversation
/// table.</b> "My inbox" is "the participant rows that are mine", and filtering conversations by a
/// participant predicate afterwards would scan conversations the member is not in to decide they
/// are not in them. Starting from the owned collection means the index on
/// <c>(user_id, is_archived, left_at)</c> answers the whole of the access decision.
/// </para>
/// </summary>
public sealed class ConversationRepository : IConversationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ConversationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<Conversation> Conversations => _dbContext.Set<Conversation>();

    public async Task<Conversation?> GetAsync(
        ConversationId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Conversations
            .Include(conversation => conversation.Participants)
            .FirstOrDefaultAsync(conversation => conversation.Id == id, cancellationToken);
    }

    public async Task<Conversation?> GetForParticipantAsync(
        ConversationId id, UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        var conversation = await GetAsync(id, cancellationToken);

        // The access decision is made here, on the loaded aggregate, rather than pushed into the
        // predicate. It costs one extra row read on a refusal and it means the rule lives beside the
        // domain method that defines it — a predicate would be a second definition of "active
        // participant" that nothing keeps in step with the first.
        return conversation is not null && conversation.HasActiveParticipant(userId)
            ? conversation
            : null;
    }

    public async Task<Conversation?> FindByReferenceAsync(
        ConversationReference reference,
        UserId initiatorUserId,
        UserId recipientUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(initiatorUserId);
        ArgumentNullException.ThrowIfNull(recipientUserId);

        // Compared as the value object, not as `.Type.Key`. The property carries a value converter
        // to a single string column, so EF can translate equality on the whole object — but it
        // cannot translate a member access *through* the converted value, which fails at runtime
        // with "could not be translated" rather than at compile time.
        var type = reference.Type;

        var candidates = await Conversations
            .Include(conversation => conversation.Participants)
            .Where(conversation =>
                conversation.Context.Type == type
                && conversation.Context.EntityId == reference.EntityId
                && conversation.Status != ConversationStatus.Closed)
            .ToListAsync(cancellationToken);

        // The pair check runs in memory over a handful of rows. Expressing "these exact two people
        // are both active participants" in SQL over an owned collection needs two correlated
        // sub-queries, and the index above has already reduced the candidates to the conversations
        // about one entity — which is bounded by how many people enquired about it.
        return candidates.Find(conversation =>
            conversation.HasActiveParticipant(initiatorUserId)
            && conversation.HasActiveParticipant(recipientUserId));
    }

    public async Task<CursorPagedResult<Conversation>> ListForUserAsync(
        ConversationQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = BuildQuery(filter);

        var cursor = ConversationCursor.Parse(filter.Cursor);

        if (cursor is not null)
        {
            var boundary = cursor.Timestamp;
            var exclusion = CursorBoundary.Exclusion(cursor.BoundaryKey, ConversationId.From);

            // Exclusion by set membership, not by comparing identifiers. An ordering comparison on a
            // value-converted strongly typed id does not translate on any provider — the platform
            // analyser refuses it at build time, and it fails outright on SQLite. See CursorBoundary.
            query = exclusion.IsExhausted
                ? query.Where(conversation => conversation.LastActivityAt < boundary)
                : query.Where(conversation =>
                    conversation.LastActivityAt < boundary
                    || (conversation.LastActivityAt == boundary
                        && !exclusion.Ids.Contains(conversation.Id)));
        }

        var limit = filter.EffectiveLimit;

        var page = await query
            .OrderByDescending(conversation => conversation.LastActivityAt)
            .ThenByDescending(conversation => conversation.Id)
            .Take(limit + 1)
            .Include(conversation => conversation.Participants)
            .ToListAsync(cancellationToken);

        var hasMore = page.Count > limit;
        var items = hasMore ? page[..limit] : page;

        // Pinning is applied after the page is drawn rather than inside the ORDER BY, and that is a
        // deliberate limitation recorded in the API guide: pin state lives on the participant row,
        // so ordering by it in SQL would need a correlated sub-query per row. With a five-pin
        // ceiling and a thirty-row page the first page always contains every pin, which is the only
        // page a client sorts.
        var ordered = items
            .OrderByDescending(conversation =>
                conversation.ParticipantFor(filter.UserId) is { IsPinned: true })
            .ThenBy(conversation => conversation.ParticipantFor(filter.UserId)?.PinOrder ?? int.MaxValue)
            .ThenByDescending(conversation => conversation.LastActivityAt)
            .ToList();

        var next = hasMore && items.Count > 0
            ? ConversationCursor
                .Create(
                    items[^1].LastActivityAt,
                    CursorBoundary.Key(
                        items,
                        conversation => conversation.LastActivityAt,
                        conversation => conversation.Id.Value,
                        cursor?.BoundaryKey,
                        cursor?.Timestamp))
                .Encode()
            : null;

        return CursorPagedResult<Conversation>.Create(ordered, next, hasMore);
    }

    public Task<int> CountPinnedAsync(UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        return Conversations
            .Where(conversation => conversation.Participants.Any(participant =>
                participant.UserId == userId
                && participant.LeftAt == null
                && participant.IsPinned))
            .CountAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(
        UserId userId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        // Muted and archived conversations are excluded, matching the client's own
        // selectTotalUnread. A badge that counted muted threads would make muting look broken.
        return Conversations
            .Where(conversation => conversation.Status != ConversationStatus.Closed)
            .SelectMany(conversation => conversation.Participants)
            .Where(participant =>
                participant.UserId == userId
                && participant.LeftAt == null
                && !participant.IsArchived
                && !(participant.IsMuted && (participant.MutedUntil == null || participant.MutedUntil > now)))
            .SumAsync(participant => participant.UnreadCount, cancellationToken);
    }

    /// <summary>
    /// The attention sweep's work list — B23.x Wave 12.
    /// <para>
    /// Tracked rather than <c>AsNoTracking</c>, because the caller marks the participants it
    /// reported and saves in the same unit of work. Participants are included for the reason the
    /// interface gives: whether somebody is owed a notice is the aggregate's rule, and answering it
    /// in a <c>Where</c> here would be a second copy of it.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Conversation>> ListQuietAsync(
        DateTimeOffset quietBefore, int limit, CancellationToken cancellationToken = default) =>
        await Conversations
            .Include(conversation => conversation.Participants)
            .Where(conversation =>
                conversation.Status == ConversationStatus.Active &&
                conversation.LastMessageAt != null &&
                conversation.LastMessageAt < quietBefore)
            .OrderBy(conversation => conversation.LastMessageAt)
            .ThenBy(conversation => conversation.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Standing unread, per member — B23.x Wave 12.
    /// <para>
    /// One grouped query rather than a walk: a sweep that loaded every conversation with an unread
    /// side would load most of the platform's inbox to publish a handful of facts.
    /// </para>
    /// <para>
    /// The exclusions are <see cref="CountUnreadAsync"/>'s, exactly — closed, left, archived, muted —
    /// so a reminder can never count a message the member's own badge does not.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ConversationUnreadBacklog>> ListUnreadBacklogAsync(
        int minimumUnread,
        DateTimeOffset unreadSince,
        DateTimeOffset notNotifiedSince,
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var rows = await Conversations
            .Where(conversation => conversation.Status != ConversationStatus.Closed)
            .SelectMany(conversation => conversation.Participants,
                (conversation, participant) => new { conversation, participant })
            .Where(row =>
                row.participant.UnreadCount > 0
                && row.participant.LeftAt == null
                && !row.participant.IsArchived
                && !(row.participant.IsMuted
                     && (row.participant.MutedUntil == null || row.participant.MutedUntil > now))

                // Long enough that it is a backlog rather than a message that arrived a minute ago.
                && row.conversation.LastMessageAt != null
                && row.conversation.LastMessageAt < unreadSince

                // The cooldown. Unread is a standing state rather than an event, so the honest
                // cadence is "not again for a while" rather than "once ever".
                && (row.participant.UnreadNoticedAt == null
                    || row.participant.UnreadNoticedAt < notNotifiedSince))
            .GroupBy(row => row.participant.UserId)
            .Select(group => new
            {
                MemberUserId = group.Key,
                UnreadCount = group.Sum(row => row.participant.UnreadCount),
                ConversationCount = group.Count(),
                OldestUnreadAt = group.Min(row => row.conversation.LastMessageAt)
            })
            .Where(row => row.UnreadCount >= minimumUnread)
            .OrderByDescending(row => row.UnreadCount)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new ConversationUnreadBacklog(
                row.MemberUserId.Value,
                row.UnreadCount,
                row.ConversationCount,
                row.OldestUnreadAt ?? now))
        ];
    }

    /// <summary>
    /// Starts the unread cooldown — B23.x Wave 12.
    /// <para>
    /// Loads the conversations these members are behind on, tracked, and moves the marker through the
    /// aggregate. Never <c>ExecuteUpdateAsync</c>: participation is an owned entity and a set update
    /// would write past the aggregate that owns the invariant, which is the shape this platform's
    /// persistence guardrail reports.
    /// </para>
    /// </summary>
    public async Task MarkUnreadReportedAsync(
        IReadOnlyList<Guid> memberUserIds,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(memberUserIds);

        if (memberUserIds.Count == 0)
        {
            return;
        }

        var members = memberUserIds.Select(UserId.From).ToList();

        var affected = await Conversations
            .Include(conversation => conversation.Participants)
            .Where(conversation =>
                conversation.Status != ConversationStatus.Closed &&
                conversation.Participants.Any(participant =>
                    members.Contains(participant.UserId) &&
                    participant.LeftAt == null &&
                    participant.UnreadCount > 0))
            .ToListAsync(cancellationToken);

        foreach (var conversation in affected)
        {
            foreach (var member in members)
            {
                conversation.NoteUnreadReported(member, at);
            }

            Update(conversation);
        }
    }

    public async Task<IReadOnlyList<Conversation>> ListByReferenceAsync(
        ConversationReference reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var type = reference.Type;

        return await Conversations
            .Include(conversation => conversation.Participants)
            .Where(conversation =>
                conversation.Context.Type == type
                && conversation.Context.EntityId == reference.EntityId
                && conversation.Status != ConversationStatus.Closed)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ShareConversationAsync(
        UserId first, UserId second, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return Conversations
            .AnyAsync(
                conversation =>
                    conversation.Status != ConversationStatus.Closed
                    && conversation.Participants.Any(participant =>
                        participant.UserId == first && participant.LeftAt == null)
                    && conversation.Participants.Any(participant =>
                        participant.UserId == second && participant.LeftAt == null),
                cancellationToken);
    }

    public void Add(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        Conversations.Add(conversation);
    }

    public void Update(Conversation conversation)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        // Attached and tracked in every path this module uses: the aggregate was loaded through one
        // of the reads above and EF already knows about it. Calling Update on an Added entity flips
        // it to Modified and skips the parent insert, which surfaces as a foreign-key violation
        // naming the participants table — so this deliberately does nothing when EF is already
        // tracking the graph.
        if (_dbContext.Entry(conversation).State == EntityState.Detached)
        {
            Conversations.Update(conversation);
        }
    }

    private IQueryable<Conversation> BuildQuery(ConversationQueryFilter filter)
    {
        var userId = filter.UserId;

        var query = Conversations.Where(conversation =>
            conversation.Participants.Any(participant =>
                participant.UserId == userId && participant.LeftAt == null));

        query = filter.ArchivedOnly
            ? query.Where(conversation => conversation.Participants.Any(participant =>
                participant.UserId == userId && participant.IsArchived))
            : query.Where(conversation => conversation.Participants.Any(participant =>
                participant.UserId == userId && !participant.IsArchived));

        // ── A conversation nobody has written in is the opener's alone ──────────────────────
        //
        // The client opens a conversation the moment a member taps "Message" on a listing, before
        // they type anything. One who then leaves without sending left a thread in the *other*
        // person's inbox reading "Start a conversation" — a seller on 15 Sep 2026 had two of
        // them from the same buyer about the same basket, and no way to tell what they were. It
        // reaches the recipient with its first message, which is when there is something to read.
        query = query.Where(conversation =>
            conversation.MessageCount > 0
            || conversation.Participants.Any(participant =>
                participant.UserId == userId && participant.Role == ParticipantRole.Initiator));

        if (filter.UnreadOnly)
        {
            query = query.Where(conversation => conversation.Participants.Any(participant =>
                participant.UserId == userId && participant.UnreadCount > 0));
        }

        if (filter.MutedOnly)
        {
            query = query.Where(conversation => conversation.Participants.Any(participant =>
                participant.UserId == userId && participant.IsMuted));
        }

        if (filter.PinnedOnly)
        {
            query = query.Where(conversation => conversation.Participants.Any(participant =>
                participant.UserId == userId && participant.IsPinned));
        }

        query = filter.RequestsOnly
            ? query.Where(conversation => conversation.Status == ConversationStatus.PendingRequest)
            : query.Where(conversation => conversation.Status != ConversationStatus.Closed);

        if (filter.ContextTypes.Count > 0)
        {
            // Projected to value objects for the same reason as above: Contains over the converted
            // type translates, a member access through it does not.
            var types = filter.ContextTypes.Select(ConversationType.From).ToList();

            query = query.Where(conversation => types.Contains(conversation.Context.Type));
        }

        if (!string.IsNullOrWhiteSpace(filter.EntityId))
        {
            query = query.Where(conversation => conversation.Context.EntityId == filter.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();

            // Titles only. Searching message bodies from the inbox query would mean joining the
            // largest table in the module to draw a list; that is what the search engine port is
            // for, and it takes the searcher so it can scope what it reads.
            query = query.Where(conversation =>
                EF.Functions.Like(conversation.Context.EntityTitle, $"%{term}%"));
        }

        return query;
    }
}
