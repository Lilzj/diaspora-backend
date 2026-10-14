using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Neighborhoods;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Persistence.Paging;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// EF Core repository for community insights and their endorsements.
/// <para>
/// <b>The default of every read path is approved-only, and the filter has to opt out of it.</b>
/// An unapproved insight is a sentence about a real residential area that nobody has checked, and
/// the safe direction to fail in is invisibility.
/// </para>
/// </summary>
public sealed class NeighborhoodInsightRepository : INeighborhoodInsightRepository
{
    private readonly DiasporaDbContext _dbContext;

    public NeighborhoodInsightRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<NeighborhoodInsight> Insights => _dbContext.Set<NeighborhoodInsight>();

    private DbSet<NeighborhoodInsightEndorsement> Endorsements =>
        _dbContext.Set<NeighborhoodInsightEndorsement>();

    public async Task<NeighborhoodInsight?> GetAsync(
        NeighborhoodInsightId id, CancellationToken cancellationToken = default) =>
        await Insights.FirstOrDefaultAsync(insight => insight.Id == id, cancellationToken);

    public async Task<NeighborhoodInsight?> GetForReadAsync(
        NeighborhoodInsightId id, CancellationToken cancellationToken = default) =>
        await Insights.AsNoTracking()
            .FirstOrDefaultAsync(insight => insight.Id == id, cancellationToken);

    public async Task<CursorPagedResult<NeighborhoodInsight>> QueryAsync(
        NeighborhoodInsightQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Apply(Insights.AsNoTracking(), filter);
        var parsed = HousingCursor.Parse(cursor);

        // Both orderings tie, and both tie hard. Endorsement counts are small integers, so a
        // thousand insights on one neighbourhood share about ten distinct values; CreatedAt is
        // stamped by the persistence interceptor, so everything written in one unit of work shares
        // an instant. A strict comparison on either drops the whole tie group, and the insight
        // nobody is ever shown is a member who wrote something and was never answered.
        //
        // The identifier is value-converted and cannot be compared in SQL, so the cursor carries
        // the identifiers already returned at the boundary and the query excludes them —
        // CursorBoundary is the platform primitive for precisely this, and it is why an insight
        // flood pages cleanly.
        if (filter.OrderByEndorsement)
        {
            if (parsed?.AsNumber() is { } boundary)
            {
                var count = (int)boundary;
                var tie = CursorBoundary.Exclusion(parsed.LastKey, NeighborhoodInsightId.From);
                var seen = tie.Ids;
                var exhausted = tie.IsExhausted;

                query = query.Where(insight =>
                    insight.EndorsementCount < count
                    || (insight.EndorsementCount == count
                        && !exhausted && !seen.Contains(insight.Id)));
            }

            query = query
                .OrderByDescending(insight => insight.EndorsementCount)
                .ThenByDescending(insight => insight.CreatedAt)
                .ThenBy(insight => insight.Id);
        }
        else
        {
            if (parsed?.AsTimestamp() is { } boundary)
            {
                var tie = CursorBoundary.Exclusion(parsed.LastKey, NeighborhoodInsightId.From);
                var seen = tie.Ids;
                var exhausted = tie.IsExhausted;

                query = query.Where(insight =>
                    insight.CreatedAt < boundary
                    || (insight.CreatedAt == boundary
                        && !exhausted && !seen.Contains(insight.Id)));
            }

            query = query
                .OrderByDescending(insight => insight.CreatedAt)
                .ThenBy(insight => insight.Id);
        }

        var effectiveLimit = Math.Clamp(limit, 1, HousingLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? filter.OrderByEndorsement
                ? HousingCursor.FromNumber(
                        rows[^1].EndorsementCount,
                        CursorBoundary.Key(
                            rows,
                            row => row.EndorsementCount,
                            row => row.Id.Value,
                            parsed?.LastKey,
                            (int?)parsed?.AsNumber()))
                    .Encode()
                : HousingCursor.FromTimestamp(
                        rows[^1].CreatedAt,
                        CursorBoundary.Key(
                            rows,
                            row => row.CreatedAt,
                            row => row.Id.Value,
                            parsed?.LastKey,
                            parsed?.AsTimestamp()))
                    .Encode()
            : null;

        return CursorPagedResult<NeighborhoodInsight>.Create(rows, next, hasMore);
    }

    public async Task<int> CountApprovedForAreaAsync(
        Guid areaId, CancellationToken cancellationToken = default) =>
        areaId == Guid.Empty
            ? 0
            : await Insights.AsNoTracking().CountAsync(
                insight => insight.AreaId == areaId
                    && insight.Status == NeighborhoodInsightStatus.Approved,
                cancellationToken);

    public async Task<bool> HasDuplicateAsync(
        Guid areaId,
        UserId authorId,
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        if (areaId == Guid.Empty || string.IsNullOrWhiteSpace(fingerprint))
        {
            return false;
        }

        // Retired insights are excluded: an observation that was true and has stopped being true
        // is one a member should be able to make again if it starts being true once more.
        return await Insights.AsNoTracking().AnyAsync(
            insight => insight.AreaId == areaId
                && insight.AuthorId == authorId
                && insight.Fingerprint == fingerprint
                && insight.Status != NeighborhoodInsightStatus.Retired,
            cancellationToken);
    }

    public async Task<bool> HasDuplicateNearAsync(
        UserId authorId,
        string fingerprint,
        double minLatitude,
        double maxLatitude,
        double minLongitude,
        double maxLongitude,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return false;
        }

        return await Insights.AsNoTracking().AnyAsync(
            insight => insight.AuthorId == authorId
                && insight.Fingerprint == fingerprint
                && insight.Status != NeighborhoodInsightStatus.Retired
                && insight.Latitude != null
                && insight.Longitude != null
                && insight.Latitude >= minLatitude
                && insight.Latitude <= maxLatitude
                && insight.Longitude >= minLongitude
                && insight.Longitude <= maxLongitude,
            cancellationToken);
    }

    public async Task<int> CountByAuthorSinceAsync(
        UserId authorId, DateTimeOffset since, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        return await Insights.AsNoTracking().CountAsync(
            insight => insight.AuthorId == authorId && insight.CreatedAt >= since,
            cancellationToken);
    }

    public async Task AddAsync(
        NeighborhoodInsight insight, CancellationToken cancellationToken = default) =>
        await Insights.AddAsync(insight, cancellationToken);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Endorsements
    // ════════════════════════════════════════════════════════════════════════════════════════

    public async Task<bool> HasEndorsedAsync(
        NeighborhoodInsightId insightId,
        UserId memberId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(insightId);
        ArgumentNullException.ThrowIfNull(memberId);

        return await Endorsements.AsNoTracking().AnyAsync(
            endorsement => endorsement.InsightId == insightId
                && endorsement.MemberId == memberId,
            cancellationToken);
    }

    public async Task<int> CountEndorsementsAsync(
        NeighborhoodInsightId insightId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(insightId);

        return await Endorsements.AsNoTracking()
            .CountAsync(endorsement => endorsement.InsightId == insightId, cancellationToken);
    }

    public async Task AddEndorsementAsync(
        NeighborhoodInsightEndorsement endorsement,
        CancellationToken cancellationToken = default) =>
        await Endorsements.AddAsync(endorsement, cancellationToken);

    /// <summary>
    /// Hard-deletes the endorsement. Withdrawing one is a member saying they no longer agree, and
    /// a tombstone still naming them would be a record of a retracted opinion.
    /// </summary>
    public async Task RemoveEndorsementAsync(
        NeighborhoodInsightId insightId,
        UserId memberId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(insightId);
        ArgumentNullException.ThrowIfNull(memberId);

        var existing = await Endorsements.FirstOrDefaultAsync(
            endorsement => endorsement.InsightId == insightId
                && endorsement.MemberId == memberId,
            cancellationToken);

        if (existing is not null)
        {
            Endorsements.Remove(existing);
        }
    }

    private static IQueryable<NeighborhoodInsight> Apply(
        IQueryable<NeighborhoodInsight> query, NeighborhoodInsightQueryFilter filter)
    {
        if (filter.ProfileId is { } profileId)
        {
            query = query.Where(insight => insight.ProfileId == profileId);
        }

        if (filter.AreaId is { } areaId && areaId != Guid.Empty)
        {
            query = query.Where(insight => insight.AreaId == areaId);
        }

        if (filter.AuthorId is { } authorId)
        {
            query = query.Where(insight => insight.AuthorId == authorId);
        }

        if (filter.HasBounds)
        {
            var south = filter.MinLatitude!.Value;
            var north = filter.MaxLatitude!.Value;
            var west = filter.MinLongitude!.Value;
            var east = filter.MaxLongitude!.Value;

            query = query.Where(insight =>
                insight.Latitude != null
                && insight.Longitude != null
                && insight.Latitude >= south
                && insight.Latitude <= north
                && insight.Longitude >= west
                && insight.Longitude <= east);
        }

        if (filter.Categories is { Count: > 0 })
        {
            var categories = filter.Categories.ToList();
            query = query.Where(insight => categories.Contains(insight.Category));
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(insight => statuses.Contains(insight.Status));
        }
        else if (!filter.IncludeUnapproved)
        {
            // The default, and the safe direction. See the class summary.
            query = query.Where(
                insight => insight.Status == NeighborhoodInsightStatus.Approved);
        }

        return query;
    }
}
