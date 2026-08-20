using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Persistence.Paging;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// EF Core repository for reservations.
/// <para>
/// <b><see cref="GetLapsedAsync"/> is the most important query in the file.</b> A reservation
/// nobody expires is a home nobody can let, and the index on the hold expiry is what stops the
/// sweep getting slower exactly as the platform gets busier.
/// </para>
/// </summary>
public sealed class HousingReservationRepository : IHousingReservationRepository
{
    /// <summary>
    /// The states that still keep units off the market. Mirrors
    /// <c>HousingReservationStatusTransitions.HoldsUnits</c> — restated as an array because a
    /// method call is not translatable into SQL, and kept in step by a unit test that asserts the
    /// two agree.
    /// </summary>
    private static readonly HousingReservationStatus[] HoldingStates =
    [
        HousingReservationStatus.Requested,
        HousingReservationStatus.Reserved,
        HousingReservationStatus.LeaseSigned
    ];

    private readonly DiasporaDbContext _dbContext;

    public HousingReservationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<HousingReservation> Reservations => _dbContext.Set<HousingReservation>();

    /// <summary>The states this repository considers to hold units. Exposed for the guard test.</summary>
    public static IReadOnlyList<HousingReservationStatus> HoldingStatuses => HoldingStates;

    public async Task<HousingReservation?> GetAsync(
        HousingReservationId id, CancellationToken cancellationToken = default) =>
        await Reservations
            .Include(reservation => reservation.History)
            .FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);

    public async Task<HousingReservation?> GetForReadAsync(
        HousingReservationId id, CancellationToken cancellationToken = default) =>
        await Reservations.AsNoTracking()
            .Include(reservation => reservation.History)
            .FirstOrDefaultAsync(reservation => reservation.Id == id, cancellationToken);

    public async Task<HousingReservation?> GetActiveForSeekerAsync(
        HousingListingId listingId,
        UserId seekerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(seekerUserId);

        return await Reservations
            .Include(reservation => reservation.History)
            .FirstOrDefaultAsync(
                reservation => reservation.ListingId == listingId
                    && reservation.SeekerUserId == seekerUserId
                    && HoldingStates.Contains(reservation.Status),
                cancellationToken);
    }

    public async Task<CursorPagedResult<HousingReservation>> QueryAsync(
        ReservationQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Apply(Reservations.AsNoTracking(), filter);
        var parsed = HousingCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } boundary)
        {
            // Strictly before the boundary instant, plus whatever remains of the tie group at
            // it. A plain `< boundary` drops every row sharing the instant, and the instant is
            // shared constantly: the persistence interceptor stamps CreatedAt, so everything
            // written in one unit of work ties. The identifier cannot be compared in SQL —
            // it is value-converted — so the cursor carries the ones already returned and the
            // query excludes them. CursorBoundary is the platform's answer to exactly this.
            var tie = CursorBoundary.Exclusion(parsed.LastKey, HousingReservationId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(reservation =>
                reservation.CreatedAt < boundary
                || (reservation.CreatedAt == boundary
                    && !exhausted && !seen.Contains(reservation.Id)));
        }

        query = query
            .OrderByDescending(reservation => reservation.CreatedAt)
            .ThenBy(reservation => reservation.Id);

        var effectiveLimit = Math.Clamp(limit, 1, HousingLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? HousingCursor.FromTimestamp(
                    rows[^1].CreatedAt,
                    CursorBoundary.Key(
                        rows,
                        row => row.CreatedAt,
                        row => row.Id.Value,
                        parsed?.LastKey,
                        parsed?.AsTimestamp()))
                .Encode()
            : null;

        return CursorPagedResult<HousingReservation>.Create(rows, next, hasMore);
    }

    public async Task<int> CountAsync(
        ReservationQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Apply(Reservations.AsNoTracking(), filter).CountAsync(cancellationToken);
    }

    public async Task<int> CountActiveBySeekerAsync(
        UserId seekerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seekerUserId);

        return await Reservations.CountAsync(
            reservation => reservation.SeekerUserId == seekerUserId
                && HoldingStates.Contains(reservation.Status),
            cancellationToken);
    }

    public async Task<IReadOnlyList<HousingReservation>> GetLapsedAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Reservations
            .Include(reservation => reservation.History)
            .Where(reservation =>
                HoldingStates.Contains(reservation.Status)
                && reservation.Terms.HoldExpiresAt != null
                && reservation.Terms.HoldExpiresAt <= asOf)
            .OrderBy(reservation => reservation.Terms.HoldExpiresAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        HousingReservation reservation, CancellationToken cancellationToken = default) =>
        await Reservations.AddAsync(reservation, cancellationToken);

    private static IQueryable<HousingReservation> Apply(
        IQueryable<HousingReservation> query, ReservationQueryFilter filter)
    {
        if (filter.SeekerUserId is { } seekerUserId)
        {
            query = query.Where(reservation => reservation.SeekerUserId == seekerUserId);
        }

        if (filter.LandlordId is { } landlordId)
        {
            query = query.Where(reservation => reservation.LandlordId == landlordId);
        }

        if (filter.ListingId is { } listingId)
        {
            query = query.Where(reservation => reservation.ListingId == listingId);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(reservation => statuses.Contains(reservation.Status));
        }

        if (filter.HoldingUnitsOnly)
        {
            query = query.Where(reservation => HoldingStates.Contains(reservation.Status));
        }

        return query;
    }
}
