using Diaspora.Modules.Providers.Domain.Providers;

namespace Diaspora.Modules.Providers.Extensibility.Ranking;

/// <summary>
/// Most recently published first.
/// <para>
/// The one ordering with no judgement in it, which is exactly why it earns a place: it is what a
/// member wants when they are looking for what is <em>new</em> in their area, and it is the only
/// ordering that gives a provider who joined this morning any chance of being seen at all. Every
/// other strategy here rewards accumulated evidence, and a directory where accumulated evidence
/// is the only currency never lets anybody new accumulate any.
/// </para>
/// </summary>
public sealed class RecencyRanking : IProviderRankingStrategy
{
    public string Key => RankingKeys.Recency;

    public string Module => ProvidersModuleMetadata.Name;

    public IReadOnlyList<Provider> Rank(
        IReadOnlyList<Provider> candidates, ProviderRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        return candidates
            .OrderByDescending(provider => provider.PublishedAt ?? provider.CreatedAt)
            .ThenBy(provider => provider.Id.Value)
            .ToList();
    }
}
