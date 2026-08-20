using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Domain.Guides;

namespace Diaspora.Modules.Guides.Extensibility.Ranking;

/// <summary>
/// Engagement decayed by age — what is earning attention now, not what earned it once.
/// <para>
/// The formula is <see cref="GuideTrendingService"/>'s, called rather than copied: two
/// implementations of one decay curve would disagree at the margin, and the margin is exactly
/// where a trending list is decided. The instant comes from the context, so the ordering is
/// deterministic for a given moment.
/// </para>
/// </summary>
public sealed class TrendingGuideRankingStrategy : IGuideRankingStrategy
{
    public string Key => GuideRankingKeys.Trending;

    public string Module => GuidesModuleMetadata.Name;

    public IReadOnlyList<Guide> Rank(
        IReadOnlyList<Guide> candidates, GuideRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        return candidates
            .OrderByDescending(guide => GuideTrendingService.ScoreAt(
                guide, context.At, GuideTrendingService.DefaultWindowDays))
            .ThenByDescending(guide => guide.PublishedAt ?? guide.CreatedAt)
            .ThenBy(guide => guide.Id.Value)
            .ToList();
    }
}
