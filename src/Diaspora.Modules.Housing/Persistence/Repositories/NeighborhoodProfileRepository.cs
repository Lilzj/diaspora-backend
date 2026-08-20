using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// EF Core repository for neighbourhood profiles.
/// <para>
/// <b>Nearly every method takes a B4 area identifier rather than a profile identifier.</b> That is
/// the whole design showing through the data access: callers know which place they mean, and the
/// profile is a thing hanging off that place. A caller forced to resolve a profile identifier
/// first would be a caller that had to know Housing owns profiles at all — which is exactly the
/// coupling the neighbourhood directory exists to avoid.
/// </para>
/// </summary>
public sealed class NeighborhoodProfileRepository : INeighborhoodProfileRepository
{
    private readonly DiasporaDbContext _dbContext;

    public NeighborhoodProfileRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<NeighborhoodProfile> Profiles => _dbContext.Set<NeighborhoodProfile>();

    public async Task<NeighborhoodProfile?> GetAsync(
        NeighborhoodProfileId id, CancellationToken cancellationToken = default) =>
        await Graph(Profiles).FirstOrDefaultAsync(
            profile => profile.Id == id, cancellationToken);

    public async Task<NeighborhoodProfile?> GetByAreaAsync(
        Guid areaId, CancellationToken cancellationToken = default) =>
        areaId == Guid.Empty
            ? null
            : await Graph(Profiles).FirstOrDefaultAsync(
                profile => profile.AreaId == areaId, cancellationToken);

    public async Task<NeighborhoodProfile?> GetByAreaForReadAsync(
        Guid areaId, CancellationToken cancellationToken = default) =>
        areaId == Guid.Empty
            ? null
            : await Graph(Profiles.AsNoTracking()).FirstOrDefaultAsync(
                profile => profile.AreaId == areaId, cancellationToken);

    public async Task<NeighborhoodProfile?> GetBySlugAsync(
        string slug, string? countryCode = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        var normalized = slug.Trim().ToLowerInvariant();
        var query = Graph(Profiles.AsNoTracking()).Where(profile => profile.Slug == normalized);

        // A slug is unique within its country rather than globally — B4's own note about there
        // being a Springfield in twelve states. Without a country the first match is returned,
        // which is what a link with no country in it can honestly resolve to.
        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();
            query = query.Where(profile => profile.CountryCode == country);
        }

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, NeighborhoodProfile>> GetManyByAreaAsync(
        IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(areaIds);

        var keys = areaIds.Where(id => id != Guid.Empty).Distinct().ToList();

        if (keys.Count == 0)
        {
            return new Dictionary<Guid, NeighborhoodProfile>();
        }

        var rows = await Profiles.AsNoTracking()
            .Where(profile => keys.Contains(profile.AreaId))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(profile => profile.AreaId);
    }

    public async Task<CursorPagedResult<NeighborhoodProfile>> QueryAsync(
        NeighborhoodQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Apply(Graph(Profiles.AsNoTracking()), filter);
        var parsed = HousingCursor.Parse(cursor);

#pragma warning disable CA1309 // EF cannot translate the ordinal overload. See the listing repository.
        if (filter.OrderByPopularity)
        {
            if (parsed?.AsNumber() is { } boundary)
            {
                query = query.Where(profile =>
                    profile.ListingCount < (int)boundary
                    || (profile.ListingCount == (int)boundary
                        && string.Compare(profile.Slug, parsed!.LastKey) > 0));
            }

            query = query
                .OrderByDescending(profile => profile.ListingCount)
                .ThenBy(profile => profile.Slug);
        }
        else
        {
            if (parsed is not null)
            {
                query = query.Where(profile => string.Compare(profile.Slug, parsed.LastKey) > 0);
            }

            query = query.OrderBy(profile => profile.Slug);
        }
#pragma warning restore CA1309

        var effectiveLimit = Math.Clamp(limit, 1, HousingLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? filter.OrderByPopularity
                ? HousingCursor.FromNumber(rows[^1].ListingCount, rows[^1].Slug).Encode()
                : HousingCursor.Create(rows[^1].Slug, rows[^1].Slug).Encode()
            : null;

        return CursorPagedResult<NeighborhoodProfile>.Create(rows, next, hasMore);
    }

    public async Task<bool> ExistsForAreaAsync(
        Guid areaId, CancellationToken cancellationToken = default) =>
        areaId != Guid.Empty
        && await Profiles.IgnoreQueryFilters()
            .AnyAsync(profile => profile.AreaId == areaId, cancellationToken);

    public async Task AddAsync(
        NeighborhoodProfile profile, CancellationToken cancellationToken = default) =>
        await Profiles.AddAsync(profile, cancellationToken);

    /// <summary>
    /// The languages are always included. They are three or four short rows, every profile render
    /// shows them, and they are the single most relevant thing on the profile for a member
    /// deciding whether they will be understood at the chemist.
    /// </summary>
    private static IQueryable<NeighborhoodProfile> Graph(
        IQueryable<NeighborhoodProfile> query) =>
        query.Include(profile => profile.PopularLanguages);

    private static IQueryable<NeighborhoodProfile> Apply(
        IQueryable<NeighborhoodProfile> query, NeighborhoodQueryFilter filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(profile => profile.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.AreaPathPrefix))
        {
            // B4's materialised path as an indexed prefix match: every neighbourhood under a city.
            var prefix = filter.AreaPathPrefix;

            query = query.Where(profile =>
                profile.AreaPath != null && profile.AreaPath.StartsWith(prefix));
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            var city = filter.CityName.Trim();
            query = query.Where(profile => profile.CityName == city);
        }

        if (filter.ApprovedOnly)
        {
            query = query.Where(profile => profile.IsApproved);
        }

        if (filter.WithListingsOnly)
        {
            query = query.Where(profile => profile.ListingCount > 0);
        }

        if (filter.WithContentOnly)
        {
            // Translated from the aggregate's HasContent, which is not itself translatable. Kept
            // in step by a unit test that asserts the two agree.
            query = query.Where(profile =>
                profile.ApprovedInsightCount > 0
                || profile.Narrative.Description != null
                || profile.Narrative.Overview != null
                || profile.Narrative.SafetySummary != null
                || profile.Narrative.TransportSummary != null
                || profile.Narrative.CostOfLivingSummary != null
                || profile.Narrative.CommunitySummary != null
                || profile.Narrative.ClimateNotes != null);
        }

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            var term = filter.Text.Trim();

            query = query.Where(profile =>
                EF.Functions.Like(profile.Name, $"%{term}%")
                || (profile.CityName != null
                    && EF.Functions.Like(profile.CityName, $"%{term}%")));
        }

        return query;
    }
}
