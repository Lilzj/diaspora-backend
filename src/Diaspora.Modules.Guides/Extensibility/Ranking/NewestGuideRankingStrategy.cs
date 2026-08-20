using Diaspora.Modules.Guides.Domain.Guides;

namespace Diaspora.Modules.Guides.Extensibility.Ranking;

/// <summary>
/// Most recently published first.
/// <para>
/// The one ordering with no judgement in it, which is exactly why it is the default: it is
/// what a member wants when they ask what is <em>new</em>, and it is the only ordering that
/// gives a guide published this morning any chance of being read at all. Every other strategy
/// here rewards accumulated evidence, and a knowledge base where accumulated evidence is the
/// only currency never lets anything new accumulate any.
/// </para>
/// </summary>
public sealed class NewestGuideRankingStrategy : IGuideRankingStrategy
{
    public string Key => GuideRankingKeys.Newest;

    public string Module => GuidesModuleMetadata.Name;

    public IReadOnlyList<Guide> Rank(
        IReadOnlyList<Guide> candidates, GuideRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        return candidates
            .OrderByDescending(guide => guide.PublishedAt ?? guide.CreatedAt)
            .ThenBy(guide => guide.Id.Value)
            .ToList();
    }
}
