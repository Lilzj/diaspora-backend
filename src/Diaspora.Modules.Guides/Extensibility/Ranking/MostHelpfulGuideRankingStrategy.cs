using Diaspora.Modules.Guides.Domain.Guides;

namespace Diaspora.Modules.Guides.Extensibility.Ranking;

/// <summary>
/// Best helpfulness record first.
/// <para>
/// The score is helpful votes minus not-helpful, damped by how few votes there are — a guide
/// at 3–0 must not outrank one at 80–5 on ratio alone, because three readers is an anecdote
/// and eighty-five is evidence. The damping is a fixed prior rather than a clever one, so a
/// member asking "why is this first" can be given an answer.
/// </para>
/// </summary>
public sealed class MostHelpfulGuideRankingStrategy : IGuideRankingStrategy
{
    /// <summary>The phantom votes the prior adds. Ten is enough to hold anecdotes down.</summary>
    private const double PriorVotes = 10d;

    public string Key => GuideRankingKeys.MostHelpful;

    public string Module => GuidesModuleMetadata.Name;

    public IReadOnlyList<Guide> Rank(
        IReadOnlyList<Guide> candidates, GuideRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        return candidates
            .OrderByDescending(Score)
            .ThenByDescending(guide => guide.Engagement.HelpfulCount)
            .ThenBy(guide => guide.Id.Value)
            .ToList();
    }

    private static double Score(Guide guide)
    {
        var helpful = guide.Engagement.HelpfulCount;
        var total = helpful + guide.Engagement.NotHelpfulCount;

        return (helpful - guide.Engagement.NotHelpfulCount) / (total + PriorVotes);
    }
}
