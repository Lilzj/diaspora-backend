using Diaspora.Modules.Recommendations.Extensibility.Engine;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;

namespace Diaspora.Modules.Recommendations.Extensibility.Diversity;

/// <summary>
/// Stops a page from being twenty of the same thing.
/// <para>
/// <b>A separate pass rather than a strategy, because it is the only step that reads the page
/// instead of the pool.</b> Every strategy scores a candidate on its own merits; whether an item
/// should be demoted because three items from the same employer are already above it is a question
/// about the assembled page, which no strategy is in a position to answer.
/// </para>
/// <para>
/// <b>Not optional, and the reason is that a popularity-weighted ranking degenerates without
/// it.</b> The most-viewed listing gets more views for being shown, which makes it more popular,
/// which shows it more — and within weeks the marketplace surface in a country is the same six
/// items for everybody. Diversity is the damping term, and the analytics rollup measures whether it
/// is working.
/// </para>
/// <para>
/// <b>It demotes and reorders; it does not remove.</b> A page of six items where five are from one
/// employer should show four of them rather than one, and a diversifier that excluded would leave
/// the page shorter than the platform had content for.
/// </para>
/// </summary>
public interface IRecommendationDiversifier
{
    /// <summary>
    /// Reorders a ranked page.
    /// <para>
    /// Returns the same items, possibly rescored and certainly reordered. Never returns fewer.
    /// </para>
    /// </summary>
    IReadOnlyList<ScoredRecommendation> Diversify(
        IReadOnlyList<ScoredRecommendation> ranked, PersonalizationContext context, int limit);
}
