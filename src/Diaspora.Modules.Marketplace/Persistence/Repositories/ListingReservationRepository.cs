using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Reservations;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>
/// EF Core repository for reservations.
/// <para>
/// <b><see cref="GetLapsedAsync"/> is the most consequential read in the module.</b> Every row it
/// misses is stock nobody can buy and nobody owns, held by a buyer who never turned up. It is
/// ordered by the collection window and served by an index on it, so a busy marketplace does not
/// make the sweep slower.
/// </para>
/// </summary>
public sealed class ListingReservationRepository : IListingReservationRepository
{
    private static readonly ReservationStatus[] LiveStatuses =
        [ReservationStatus.Pending, ReservationStatus.AwaitingCollection];

    private readonly DiasporaDbContext _dbContext;

    public ListingReservationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ListingReservation> Reservations => _dbContext.Set<ListingReservation>();

    public async Task<ListingReservation?> GetAsync(
        ListingReservationId id, CancellationToken cancellationToken = default) =>
        await Reservations.Include(reservation => reservation.History)
            .FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);

    public async Task<ListingReservation?> GetForReadAsync(
        ListingReservationId id, CancellationToken cancellationToken = default) =>
        await Reservations.AsNoTracking().Include(reservation => reservation.History)
            .FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);

    public async Task<ListingReservation?> GetActiveForBuyerAsync(
        MarketplaceListingId listingId,
        UserId buyerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(buyerUserId);

        return await Reservations.Include(reservation => reservation.History)
            .FirstOrDefaultAsync(
                reservation => reservation.ListingId == listingId
                    && reservation.BuyerUserId == buyerUserId
                    && LiveStatuses.Contains(reservation.Status),
                cancellationToken);
    }

    public async Task<CursorPagedResult<ListingReservation>> QueryAsync(
        ReservationQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var parsed = MarketplaceCursor.Parse(cursor);

        var query = ApplyCursor(
            Apply(Reservations.AsNoTracking().Include(reservation => reservation.History), filter),
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
                rows[^1].CreatedAt,
                CursorBoundary.Key(
                    rows,
                    reservation => reservation.CreatedAt,
                    reservation => reservation.Id.Value,
                    parsed?.LastKey,
                    parsed?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<ListingReservation>.Create(rows, next, hasMore);
    }

    public async Task<int> CountAsync(
        ReservationQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Apply(Reservations.AsNoTracking(), filter).CountAsync(cancellationToken);
    }

    public async Task<int> CountActiveByBuyerAsync(
        UserId buyerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(buyerUserId);

        return await Reservations.AsNoTracking()
            .CountAsync(
                reservation => reservation.BuyerUserId == buyerUserId
                    && LiveStatuses.Contains(reservation.Status),
                cancellationToken);
    }

    public async Task<IReadOnlyList<ListingReservation>> GetLapsedAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Reservations.Include(reservation => reservation.History)
            .Where(reservation =>
                reservation.Window.Until != null
                && reservation.Window.Until <= asOf
                && LiveStatuses.Contains(reservation.Status))
            .OrderBy(reservation => reservation.Window.Until)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ListingReservation>> GetActiveForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Reservations.Include(reservation => reservation.History)
            .Where(reservation =>
                reservation.ListingId == listingId
                && LiveStatuses.Contains(reservation.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        ListingReservation reservation, CancellationToken cancellationToken = default) =>
        await Reservations.AddAsync(reservation, cancellationToken);

    private static IQueryable<ListingReservation> Apply(
        IQueryable<ListingReservation> query, ReservationQueryFilter filter)
    {
        if (filter.ListingId is { } listingId)
        {
            query = query.Where(reservation => reservation.ListingId == listingId);
        }

        if (filter.SellerId is { } sellerId)
        {
            query = query.Where(reservation => reservation.SellerId == sellerId);
        }

        if (filter.BuyerUserId is { } buyerUserId)
        {
            query = query.Where(reservation => reservation.BuyerUserId == buyerUserId);
        }

        if (filter.ActiveOnly)
        {
            query = query.Where(reservation => LiveStatuses.Contains(reservation.Status));
        }
        else if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(reservation => statuses.Contains(reservation.Status));
        }

        return query;
    }

    /// <summary>
    /// Newest first, tie-broken by identifier through <see cref="CursorBoundary"/>.
    /// <para>
    /// A reservation has no slug, so the tie-break cannot be a comparable string column — and the
    /// identifier is value-converted, so neither comparing it nor reaching through to
    /// <c>Id.Value</c> translates. The cursor carries the identifiers already returned at the
    /// boundary instant and the query excludes them; ordering on the column translates fine.
    /// </para>
    /// <para>
    /// The tie group is real: <c>CreatedAt</c> is stamped by the shared persistence interceptor, so
    /// every reservation written in one unit of work carries an identical instant.
    /// </para>
    /// </summary>
    private static IQueryable<ListingReservation> ApplyCursor(
        IQueryable<ListingReservation> query, MarketplaceCursor? cursor)
    {
        if (cursor?.AsTimestamp() is { } value)
        {
            var tie = CursorBoundary.Exclusion(cursor.LastKey, ListingReservationId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(reservation =>
                reservation.CreatedAt < value
                || (reservation.CreatedAt == value
                    && !exhausted && !seen.Contains(reservation.Id)));
        }

        return query
            .OrderByDescending(reservation => reservation.CreatedAt)
            .ThenBy(reservation => reservation.Id);
    }
}
