using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Identifiers;
using Diaspora.Modules.Providers.Domain.Providers;
using Diaspora.Modules.Providers.Extensibility.Recommendations;

namespace Diaspora.Modules.Providers.Infrastructure.Recommendations;

/// <summary>
/// The "you might also like" heuristic B7 ships.
/// <para>
/// <b>Honest about being a heuristic.</b> Same category, same country, well rated, not the one
/// you are looking at. It is not a recommender and does not pretend to be — the brief forbids
/// implementing complex recommendation algorithms, and B17 will register something better at a
/// lower order and take over without this file changing.
/// </para>
/// <para>
/// It exists because the client's provider screen already has a "Similar Providers" section, and
/// what fills it today is
/// <c>mockListings.filter(l =&gt; l.listingId !== current).slice(0, 2)</c> — literally the first
/// two other providers in the array, regardless of category, city or quality. Anything
/// category-aware is a large improvement over that, and the bar for shipping it is correspondingly
/// low.
/// </para>
/// <para>
/// Returns identifiers rather than aggregates, per the seam's contract, so B7 re-applies
/// visibility before anything reaches a screen. A recommender that returned providers could
/// surface a private or suspended one.
/// </para>
/// </summary>
public sealed class SimilarCategoryRecommendations : IProviderRecommendationProvider
{
    private readonly IProviderRepository _providers;

    public SimilarCategoryRecommendations(IProviderRepository providers) => _providers = providers;

    public string Module => ProvidersModuleMetadata.Name;

    public int Order => 1_000;

    public async Task<IReadOnlyList<ProviderId>> RecommendAsync(
        RecommendationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Without a category there is nothing to be similar to. Returning nothing is better than
        // returning arbitrary providers, which is exactly what the client does today.
        if (request.CategoryId is not { } categoryId)
        {
            return [];
        }

        var page = await _providers.QueryAsync(
            new ProviderQueryFilter
            {
                Statuses = [ProviderStatus.Active],
                AllowedVisibility = [ProviderVisibilityLevel.Public, ProviderVisibilityLevel.Members],
                CategoryId = categoryId,
                CountryCode = request.CountryCode,
                AreaId = request.AreaId,

                // Two reviews is a low bar, and it is the point: a directory that only ever
                // recommends established providers never lets a new one become established.
                MinimumReviewCount = 2,
                Sort = ProviderSortOrder.Rating,

                // Over-fetch, because the exclusions are applied after the query — the seed
                // provider and anything already on screen would otherwise eat into the page.
                Limit = Math.Min(
                    request.Limit + request.Exclude.Count + 1,
                    Domain.Policies.ProviderLimits.MaxPageSize)
            },
            cancellationToken);

        var excluded = request.Exclude.Select(id => id.Value).ToHashSet();

        if (request.SeedProviderId is { } seed)
        {
            excluded.Add(seed.Value);
        }

        return page.Items
            .Where(provider => !excluded.Contains(provider.Id.Value))
            .Take(request.Limit)
            .Select(provider => provider.Id)
            .ToList();
    }
}
