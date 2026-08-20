using Diaspora.Modules.Providers.Domain.Providers;

namespace Diaspora.Modules.Providers.Extensibility.Ranking;

/// <summary>
/// Nearest first. The client's "Distance" sort.
/// <para>
/// Every distance comes from Location, pre-computed for the page and handed in on the context.
/// This strategy does no geometry, which is the whole of B7's compliance with "never duplicate
/// location logic".
/// </para>
/// <para>
/// <b>Providers with no distance go last, not first.</b> A remote adviser and a provider whose
/// coordinates are unknown both have no distance, and the natural sort of a null is ahead of
/// every real value — which would put exactly the providers a member is <em>not</em> sorting by
/// distance for at the top of a distance sort. They keep their place at the end, ordered by the
/// balanced score so the tail is not arbitrary.
/// </para>
/// </summary>
public sealed class ProximityRanking : IProviderRankingStrategy
{
    public string Key => RankingKeys.Proximity;

    public string Module => ProvidersModuleMetadata.Name;

    public IReadOnlyList<Provider> Rank(
        IReadOnlyList<Provider> candidates, ProviderRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        return candidates
            .OrderBy(provider =>
                context.DistancesKm.TryGetValue(provider.Id.Value, out var distance)
                    ? distance
                    : double.MaxValue)
            .ThenByDescending(provider => provider.ReviewSummary.Average)
            .ThenBy(provider => provider.Id.Value)
            .ToList();
    }
}
