using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Offers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>
/// EF Core repository for negotiations.
/// <para>
/// <b>Commands load the history and lists do not.</b> Every move appends an entry and the aggregate
/// numbers rounds from what it holds, so a command that loaded a negotiation without its history
/// would restart the round count at one and silently reset the ceiling. A dashboard listing two
/// hundred negotiations, on the other hand, must not pull two thousand history rows to render two
/// hundred amounts.
/// </para>
/// </summary>
public sealed class ListingOfferRepository : IListingOfferRepository
{
    private static readonly OfferStatus[] OpenStatuses =
        [OfferStatus.Pending, OfferStatus.Countered];

    private readonly DiasporaDbContext _dbContext;

    public ListingOfferRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ListingOffer> Offers => _dbContext.Set<ListingOffer>();

    public async Task<ListingOffer?> GetAsync(
        ListingOfferId id, CancellationToken cancellationToken = default) =>
        await Offers.Include(offer => offer.History)
            .FirstOrDefaultAsync(offer => offer.Id == id, cancellationToken);

    public async Task<ListingOffer?> GetForReadAsync(
        ListingOfferId id, CancellationToken cancellationToken = default) =>
        await Offers.AsNoTracking().Include(offer => offer.History)
            .FirstOrDefaultAsync(offer => offer.Id == id, cancellationToken);

    public async Task<ListingOffer?> GetOpenForBuyerAsync(
        MarketplaceListingId listingId,
        UserId buyerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(buyerUserId);

        return await Offers.Include(offer => offer.History)
            .FirstOrDefaultAsync(
                offer => offer.ListingId == listingId
                    && offer.BuyerUserId == buyerUserId
                    && OpenStatuses.Contains(offer.Status),
                cancellationToken);
    }

    public async Task<CursorPagedResult<ListingOffer>> QueryAsync(
        OfferQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var parsed = MarketplaceCursor.Parse(cursor);

        var query = ApplyCursor(Apply(Offers.AsNoTracking(), filter), parsed);

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
                    offer => offer.LastActivityAt,
                    offer => offer.Id.Value,
                    parsed?.LastKey,
                    parsed?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<ListingOffer>.Create(rows, next, hasMore);
    }

    public async Task<int> CountAsync(
        OfferQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Apply(Offers.AsNoTracking(), filter).CountAsync(cancellationToken);
    }

    public async Task<int> CountByBuyerSinceAsync(
        UserId buyerUserId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buyerUserId);

        return await Offers.AsNoTracking()
            .CountAsync(
                offer => offer.BuyerUserId == buyerUserId && offer.CreatedAt >= since,
                cancellationToken);
    }

    public async Task<IReadOnlyList<ListingOffer>> GetLapsedAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Offers.Include(offer => offer.History)
            .Where(offer =>
                offer.ExpiresAt != null
                && offer.ExpiresAt <= asOf
                && OpenStatuses.Contains(offer.Status))
            .OrderBy(offer => offer.ExpiresAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ListingOffer>> GetOpenForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Offers.Include(offer => offer.History)
            .Where(offer =>
                offer.ListingId == listingId && OpenStatuses.Contains(offer.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ListingOffer offer, CancellationToken cancellationToken = default) =>
        await Offers.AddAsync(offer, cancellationToken);

    private static IQueryable<ListingOffer> Apply(
        IQueryable<ListingOffer> query, OfferQueryFilter filter)
    {
        if (filter.ListingId is { } listingId)
        {
            query = query.Where(offer => offer.ListingId == listingId);
        }

        if (filter.SellerId is { } sellerId)
        {
            query = query.Where(offer => offer.SellerId == sellerId);
        }

        if (filter.BuyerUserId is { } buyerUserId)
        {
            query = query.Where(offer => offer.BuyerUserId == buyerUserId);
        }

        if (filter.OpenOnly)
        {
            query = query.Where(offer => OpenStatuses.Contains(offer.Status));
        }
        else if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(offer => statuses.Contains(offer.Status));
        }

        if (filter.AwaitingParty is { } party)
        {
            query = query.Where(offer =>
                offer.AwaitingParty == party && OpenStatuses.Contains(offer.Status));
        }

        return query;
    }

    /// <summary>
    /// Most recently active first, tie-broken by identifier.
    /// <para>
    /// A negotiation has no slug, so the tie-break cannot be a comparable string column. It is
    /// <see cref="CursorBoundary"/> instead: the cursor carries the identifiers already returned at
    /// the boundary instant and the query excludes them. Comparing identifiers is not an option —
    /// they are value-converted, and neither the comparison nor a reach-through to
    /// <c>Id.Value</c> translates. Ordering on the column does.
    /// </para>
    /// <para>
    /// The tie group here is not hypothetical: an offer's <c>LastActivityAt</c> is set when it is
    /// written, so a seeded or bulk-written batch shares one instant and the whole batch is one tie
    /// group.
    /// </para>
    /// </summary>
    private static IQueryable<ListingOffer> ApplyCursor(
        IQueryable<ListingOffer> query, MarketplaceCursor? cursor)
    {
        if (cursor?.AsTimestamp() is { } value)
        {
            var tie = CursorBoundary.Exclusion(cursor.LastKey, ListingOfferId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(offer =>
                offer.LastActivityAt < value
                || (offer.LastActivityAt == value && !exhausted && !seen.Contains(offer.Id)));
        }

        return query
            .OrderByDescending(offer => offer.LastActivityAt)
            .ThenBy(offer => offer.Id);
    }
}
