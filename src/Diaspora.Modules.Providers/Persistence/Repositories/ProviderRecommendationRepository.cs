using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Reviews;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Providers.Persistence.Repositories;

/// <summary>EF Core repository for provider recommendations.</summary>
public sealed class ProviderRecommendationRepository : IProviderRecommendationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public ProviderRecommendationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ProviderRecommendation> Recommendations =>
        _dbContext.Set<ProviderRecommendation>();

    public async Task<ProviderRecommendation?> GetAsync(
        ProviderRecommendationId id, CancellationToken cancellationToken = default) =>
        await Recommendations.FirstOrDefaultAsync(
            recommendation => recommendation.Id == id, cancellationToken);

    public async Task<ProviderRecommendation?> FindByRecommenderAsync(
        ProviderId providerId, UserId recommenderId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);
        ArgumentNullException.ThrowIfNull(recommenderId);

        return await Recommendations.FirstOrDefaultAsync(
            recommendation =>
                recommendation.ProviderId == providerId
                && recommendation.RecommenderId == recommenderId,
            cancellationToken);
    }

    public async Task<CursorPagedResult<ProviderRecommendation>> ListForProviderAsync(
        ProviderId providerId,
        string? cursor,
        int limit,
        bool featuredOnly = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerId);

        var query = Recommendations
            .AsNoTracking()
            .Where(recommendation =>
                recommendation.ProviderId == providerId && !recommendation.IsWithdrawn);

        if (featuredOnly)
        {
            query = query.Where(recommendation => recommendation.IsFeatured);
        }

        var parsed = ProviderCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } after)
        {
            var tie = CursorBoundary.Exclusion(parsed.LastSlug, ProviderRecommendationId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            // Two faults, the same pair the community feed carried. `CreatedAt < after` alone
            // drops every recommendation sharing the boundary instant. And the ordering leads
            // with IsFeatured while the cursor compared only CreatedAt, so a featured
            // recommendation — older than the boundary, and sorting first regardless of age —
            // came back at the top of every page. See CursorBoundary.
            query = parsed.LeadingFlag
                ? query.Where(recommendation =>
                    !recommendation.IsFeatured
                    || recommendation.CreatedAt < after
                    || (recommendation.CreatedAt == after
                        && !exhausted && !seen.Contains(recommendation.Id)))
                : query.Where(recommendation =>
                    !recommendation.IsFeatured
                    && (recommendation.CreatedAt < after
                        || (recommendation.CreatedAt == after
                            && !exhausted && !seen.Contains(recommendation.Id))));
        }

        var pageSize = Math.Clamp(limit, 1, 100);

        var items = await query
            .OrderByDescending(recommendation => recommendation.IsFeatured)
            .ThenByDescending(recommendation => recommendation.CreatedAt)
            .ThenBy(recommendation => recommendation.Kind)
            .ThenByDescending(recommendation => recommendation.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > pageSize;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var nextCursor = hasMore && items.Count > 0
            ? ProviderCursor.FromTimestamp(
                items[^1].CreatedAt,
                CursorBoundary.Key(
                    items,
                    recommendation => recommendation.CreatedAt,
                    recommendation => recommendation.Id.Value,
                    parsed?.LastSlug,
                    parsed?.AsTimestamp()),
                items[^1].IsFeatured).Encode()
            : null;

        return CursorPagedResult<ProviderRecommendation>.Create(items, nextCursor, hasMore);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> CountForProvidersAsync(
        IReadOnlyCollection<ProviderId> providerIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(providerIds);

        if (providerIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        var keys = providerIds.ToList();

        var rows = await Recommendations
            .AsNoTracking()
            .Where(recommendation =>
                keys.Contains(recommendation.ProviderId) && !recommendation.IsWithdrawn)
            .GroupBy(recommendation => recommendation.ProviderId)
            .Select(group => new { ProviderId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.ProviderId.Value, row => row.Count);
    }

    public async Task AddAsync(
        ProviderRecommendation recommendation, CancellationToken cancellationToken = default) =>
        await Recommendations.AddAsync(recommendation, cancellationToken);

    public void Remove(ProviderRecommendation recommendation) =>
        Recommendations.Remove(recommendation);
}
