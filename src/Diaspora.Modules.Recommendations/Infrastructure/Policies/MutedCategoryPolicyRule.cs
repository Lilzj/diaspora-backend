using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Policies;

namespace Diaspora.Modules.Recommendations.Infrastructure.Policies;

/// <summary>
/// Honour a muted category.
/// <para>
/// <b>An exclusion rather than a demotion, because a mute is an instruction rather than a
/// preference.</b> Somebody who muted a category and then sees one of its items assumes the setting
/// does not work, and they are close enough to right. The soft version of this rule exists — it is
/// the interest strategy, which reads the same preference record and scores a preferred category up
/// — and keeping the two apart is why the mute can be absolute.
/// </para>
/// <para>
/// A candidate with no category is never muted. That is not a loophole: a mute names a category, and
/// excluding uncategorised content because it might belong to a muted category would remove most of
/// the platform's cross-cutting guides from every muted member's feed.
/// </para>
/// </summary>
public sealed class MutedCategoryPolicyRule : IRecommendationPolicyRule
{
    public const string PolicyKey = "muted-category";

    public string Key => PolicyKey;

    public RecommendationPolicyKind Kind => RecommendationPolicyKind.MutedCategory;

    public PolicyDecision Evaluate(RecommendationCandidate candidate, PolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(context);

        if (candidate.CategoryKey is null)
        {
            return PolicyDecision.Allow();
        }

        var preference = context.Personalization.Preference;

        return preference?.IsCategoryMuted(candidate.CategoryKey, candidate.Target.TargetType) == true
            ? PolicyDecision.Exclude($"the member muted the '{candidate.CategoryKey}' category")
            : PolicyDecision.Allow();
    }
}
