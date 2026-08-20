using Diaspora.Modules.Marketplace.Domain.Conversations;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>EF Core repository for conversation references.</summary>
public sealed class ListingConversationRepository : IListingConversationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ListingConversationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ListingConversation> Conversations => _dbContext.Set<ListingConversation>();

    public async Task<ListingConversation?> GetAsync(
        ListingConversationId id, CancellationToken cancellationToken = default) =>
        await Conversations.FirstOrDefaultAsync(
            conversation => conversation.Id == id, cancellationToken);

    public async Task<ListingConversation?> FindAsync(
        MarketplaceListingId listingId,
        UserId buyerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(buyerUserId);

        return await Conversations.FirstOrDefaultAsync(
            conversation => conversation.ListingId == listingId
                && conversation.BuyerUserId == buyerUserId,
            cancellationToken);
    }

    /// <summary>
    /// A member's conversations, whichever side of them they are on.
    /// <para>
    /// One query with an <c>OR</c> rather than two unioned, because a member who both buys and
    /// sells wants one list ordered by recency — not a buyer list and a seller list they have to
    /// interleave themselves.
    /// </para>
    /// </summary>
    public async Task<CursorPagedResult<ListingConversation>> QueryForUserAsync(
        UserId userId,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var parsed = MarketplaceCursor.Parse(cursor);

        var query = ApplyCursor(
            Conversations.AsNoTracking()
                .Where(conversation =>
                    conversation.BuyerUserId == userId || conversation.SellerUserId == userId),
            parsed);

        var effectiveLimit = Math.Clamp(limit, 1, MarketplaceLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? MarketplaceCursor.FromTimestamp(
                rows[^1].LastActivityAt,
                CursorBoundary.Key(
                    rows,
                    conversation => conversation.LastActivityAt,
                    conversation => conversation.Id.Value,
                    parsed?.LastKey,
                    parsed?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<ListingConversation>.Create(rows, next, hasMore);
    }

    public async Task<IReadOnlyList<ListingConversation>> GetOpenForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Conversations
            .Where(conversation =>
                conversation.ListingId == listingId
                && conversation.Status != ConversationStatus.Closed)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        ListingConversation conversation, CancellationToken cancellationToken = default) =>
        await Conversations.AddAsync(conversation, cancellationToken);

    /// <summary>
    /// Most recently active first, tie-broken by identifier through <see cref="CursorBoundary"/>.
    /// <para>
    /// A conversation reference has no slug, and the identifier is value-converted — neither
    /// comparing it nor reaching through to <c>Id.Value</c> translates to SQL. The cursor carries
    /// the identifiers already returned at the boundary instant and the query excludes them.
    /// </para>
    /// </summary>
    private static IQueryable<ListingConversation> ApplyCursor(
        IQueryable<ListingConversation> query, MarketplaceCursor? cursor)
    {
        if (cursor?.AsTimestamp() is { } value)
        {
            var tie = CursorBoundary.Exclusion(cursor.LastKey, ListingConversationId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(conversation =>
                conversation.LastActivityAt < value
                || (conversation.LastActivityAt == value
                    && !exhausted && !seen.Contains(conversation.Id)));
        }

        return query
            .OrderByDescending(conversation => conversation.LastActivityAt)
            .ThenBy(conversation => conversation.Id);
    }
}
