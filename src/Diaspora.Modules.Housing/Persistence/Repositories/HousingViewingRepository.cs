using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Persistence.Paging;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// EF Core repository for viewings.
/// <para>
/// <b><see cref="HasAddressUnlockingViewingAsync"/> is the hot path.</b> Every listing detail
/// render for a signed-in member calls it, so it is an <c>ANY</c> against the composite index
/// rather than a load of the aggregate — the answer is one bit and loading a viewing graph to
/// produce it would be the module's most expensive mistake.
/// </para>
/// </summary>
public sealed class HousingViewingRepository : IHousingViewingRepository
{
    /// <summary>
    /// The states that entitle a member to the full address. Scheduled is enough: a member cannot
    /// attend an address they have not been given, so waiting for completion would make the
    /// requirement unsatisfiable. No-show is included because the address was already disclosed.
    /// </summary>
    private static readonly HousingViewingStatus[] AddressUnlocking =
    [
        HousingViewingStatus.Scheduled,
        HousingViewingStatus.Completed,
        HousingViewingStatus.NoShow
    ];

    private static readonly HousingViewingStatus[] OpenStates =
    [
        HousingViewingStatus.Requested,
        HousingViewingStatus.Scheduled
    ];

    private readonly DiasporaDbContext _dbContext;

    public HousingViewingRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<HousingViewing> Viewings => _dbContext.Set<HousingViewing>();

    public async Task<HousingViewing?> GetAsync(
        HousingViewingId id, CancellationToken cancellationToken = default) =>
        await Viewings
            .Include(viewing => viewing.History)
            .FirstOrDefaultAsync(viewing => viewing.Id == id, cancellationToken);

    public async Task<HousingViewing?> GetForReadAsync(
        HousingViewingId id, CancellationToken cancellationToken = default) =>
        await Viewings.AsNoTracking()
            .Include(viewing => viewing.History)
            .FirstOrDefaultAsync(viewing => viewing.Id == id, cancellationToken);

    public async Task<HousingViewing?> GetOpenForSeekerAsync(
        HousingListingId listingId,
        UserId seekerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(seekerUserId);

        return await Viewings
            .Include(viewing => viewing.History)
            .FirstOrDefaultAsync(
                viewing => viewing.ListingId == listingId
                    && viewing.SeekerUserId == seekerUserId
                    && OpenStates.Contains(viewing.Status),
                cancellationToken);
    }

    public async Task<bool> HasAddressUnlockingViewingAsync(
        HousingListingId listingId,
        UserId seekerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(seekerUserId);

        return await Viewings.AsNoTracking().AnyAsync(
            viewing => viewing.ListingId == listingId
                && viewing.SeekerUserId == seekerUserId
                && AddressUnlocking.Contains(viewing.Status),
            cancellationToken);
    }

    public async Task<bool> HasCompletedViewingAsync(
        HousingListingId listingId,
        UserId seekerUserId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(seekerUserId);

        return await Viewings.AsNoTracking().AnyAsync(
            viewing => viewing.ListingId == listingId
                && viewing.SeekerUserId == seekerUserId
                && viewing.Status == HousingViewingStatus.Completed,
            cancellationToken);
    }

    public async Task<CursorPagedResult<HousingViewing>> QueryAsync(
        ViewingQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Apply(Viewings.AsNoTracking(), filter);
        var parsed = HousingCursor.Parse(cursor);

        // Paged on the created timestamp with the identifier as an opaque tie-break: a viewing has
        // no slug, and the cursor guide records that this is the shape for pages that do not.
        if (parsed?.AsTimestamp() is { } boundary)
        {
            // Strictly before the boundary instant, plus whatever remains of the tie group at
            // it. A plain `< boundary` drops every row sharing the instant, and the instant is
            // shared constantly: the persistence interceptor stamps CreatedAt, so everything
            // written in one unit of work ties. The identifier cannot be compared in SQL —
            // it is value-converted — so the cursor carries the ones already returned and the
            // query excludes them. CursorBoundary is the platform's answer to exactly this.
            var tie = CursorBoundary.Exclusion(parsed.LastKey, HousingViewingId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(viewing =>
                viewing.CreatedAt < boundary
                || (viewing.CreatedAt == boundary && !exhausted && !seen.Contains(viewing.Id)));
        }

        query = query
            .OrderByDescending(viewing => viewing.CreatedAt)
            .ThenBy(viewing => viewing.Id);

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

        return CursorPagedResult<HousingViewing>.Create(rows, next, hasMore);
    }

    public async Task<int> CountAsync(
        ViewingQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Apply(Viewings.AsNoTracking(), filter).CountAsync(cancellationToken);
    }

    public async Task<int> CountOpenBySeekerAsync(
        UserId seekerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seekerUserId);

        return await Viewings.CountAsync(
            viewing => viewing.SeekerUserId == seekerUserId
                && OpenStates.Contains(viewing.Status),
            cancellationToken);
    }

    public async Task<IReadOnlyList<HousingViewing>> GetLapsedAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Viewings
            .Include(viewing => viewing.History)
            .Where(viewing =>
                OpenStates.Contains(viewing.Status)
                && viewing.Window.ConfirmedAt != null
                && viewing.Window.ConfirmedAt < asOf)
            .OrderBy(viewing => viewing.Window.ConfirmedAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(
        HousingViewing viewing, CancellationToken cancellationToken = default) =>
        await Viewings.AddAsync(viewing, cancellationToken);

    private static IQueryable<HousingViewing> Apply(
        IQueryable<HousingViewing> query, ViewingQueryFilter filter)
    {
        if (filter.SeekerUserId is { } seekerUserId)
        {
            query = query.Where(viewing => viewing.SeekerUserId == seekerUserId);
        }

        if (filter.LandlordId is { } landlordId)
        {
            query = query.Where(viewing => viewing.LandlordId == landlordId);
        }

        if (filter.ListingId is { } listingId)
        {
            query = query.Where(viewing => viewing.ListingId == listingId);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(viewing => statuses.Contains(viewing.Status));
        }

        if (filter.OpenOnly)
        {
            query = query.Where(viewing => OpenStates.Contains(viewing.Status));
        }

        if (filter.From is { } from)
        {
            query = query.Where(viewing =>
                viewing.Window.ConfirmedAt >= from || viewing.Window.PreferredAt >= from);
        }

        if (filter.Until is { } until)
        {
            query = query.Where(viewing =>
                viewing.Window.ConfirmedAt <= until || viewing.Window.PreferredAt <= until);
        }

        return query;
    }
}
