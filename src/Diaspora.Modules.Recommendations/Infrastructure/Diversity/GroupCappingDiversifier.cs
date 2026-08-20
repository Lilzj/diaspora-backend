using Diaspora.Modules.Recommendations.Extensibility.Diversity;
using Diaspora.Modules.Recommendations.Extensibility.Engine;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Options;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Recommendations.Infrastructure.Diversity;

/// <summary>
/// Caps how much of a page any one group may take.
/// <para>
/// <b>A greedy re-selection rather than a re-score, because the two produce visibly different
/// pages.</b> Scoring down every item past the cap and re-sorting leaves a page that is still mostly
/// one employer, just in a different order. Walking the ranked list and skipping an item whose group
/// is already full — then coming back for the skipped ones once the good candidates run out —
/// produces a page that is genuinely mixed and still ordered by merit.
/// </para>
/// <para>
/// <b>Three groups, and a candidate belongs to all of them.</b> The owner stops one employer or one
/// landlord taking the page; the category stops one kind of thing taking it; the target type stops
/// the home feed from being twenty jobs. Capping on only one of the three leaves the other two free
/// to dominate, which is how a feed ends up technically diverse and practically monotonous.
/// </para>
/// <para>
/// <b>Nothing is ever removed.</b> The skipped items are appended after the capped ones, so a page
/// is never shorter than the platform had content for. A diversifier that dropped items would trade
/// one bad page for a worse one.
/// </para>
/// </summary>
public sealed class GroupCappingDiversifier : IRecommendationDiversifier
{
    private readonly IOptions<RecommendationOptions> _options;

    public GroupCappingDiversifier(IOptions<RecommendationOptions> options) => _options = options;

    public IReadOnlyList<ScoredRecommendation> Diversify(
        IReadOnlyList<ScoredRecommendation> ranked, PersonalizationContext context, int limit)
    {
        ArgumentNullException.ThrowIfNull(ranked);
        ArgumentNullException.ThrowIfNull(context);

        if (ranked.Count <= 1)
        {
            return ranked;
        }

        var cap = Math.Max(1, _options.Value.MaximumItemsPerGroup);

        var selected = new List<ScoredRecommendation>(Math.Min(limit, ranked.Count));
        var deferred = new List<ScoredRecommendation>();

        var byOwner = new Dictionary<string, int>(StringComparer.Ordinal);
        var byCategory = new Dictionary<string, int>(StringComparer.Ordinal);
        var byType = new Dictionary<string, int>(StringComparer.Ordinal);

        // The type cap is proportional rather than fixed: on a single-type surface every item is the
        // same type and a fixed cap of three would defer all but three of them.
        var typeCap = Math.Max(cap, (int)Math.Ceiling(limit / 2.0));

        foreach (var item in ranked)
        {
            var owner = item.Candidate.OwnerUserId?.Value.ToString() ?? $"module:{item.Candidate.SourceModule}";
            var category = item.Candidate.CategoryKey ?? "uncategorised";
            var type = item.Candidate.Target.TargetType.ToString();

            var ownerCount = byOwner.GetValueOrDefault(owner);
            var categoryCount = byCategory.GetValueOrDefault(category);
            var typeCount = byType.GetValueOrDefault(type);

            if (ownerCount >= cap || categoryCount >= cap || typeCount >= typeCap)
            {
                deferred.Add(item);
                continue;
            }

            selected.Add(item);

            byOwner[owner] = ownerCount + 1;
            byCategory[category] = categoryCount + 1;
            byType[type] = typeCount + 1;

            if (selected.Count >= limit)
            {
                break;
            }
        }

        // The page is not full and the only candidates left are ones a cap deferred. Showing them is
        // better than a short page — see the class summary.
        if (selected.Count < limit && deferred.Count > 0)
        {
            selected.AddRange(deferred.Take(limit - selected.Count));
        }

        return selected;
    }
}
