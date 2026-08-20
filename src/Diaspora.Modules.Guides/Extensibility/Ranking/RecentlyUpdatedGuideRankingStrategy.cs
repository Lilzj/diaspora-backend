using Diaspora.Modules.Guides.Domain.Guides;

namespace Diaspora.Modules.Guides.Extensibility.Ranking;

/// <summary>
/// Most recently edited first.
/// <para>
/// Procedural knowledge rots — fees change, forms are renumbered, offices move — and the
/// guide touched last week is more likely to be right than the one untouched for two years.
/// This ordering surfaces maintenance, which is the thing a knowledge base most needs to
/// reward and the thing raw popularity never does.
/// </para>
/// </summary>
public sealed class RecentlyUpdatedGuideRankingStrategy : IGuideRankingStrategy
{
    public string Key => GuideRankingKeys.RecentlyUpdated;

    public string Module => GuidesModuleMetadata.Name;

    public IReadOnlyList<Guide> Rank(
        IReadOnlyList<Guide> candidates, GuideRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        return candidates
            .OrderByDescending(guide => guide.ModifiedAt ?? guide.PublishedAt ?? guide.CreatedAt)
            .ThenBy(guide => guide.Id.Value)
            .ToList();
    }
}
