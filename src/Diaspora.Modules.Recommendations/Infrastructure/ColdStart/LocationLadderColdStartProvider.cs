using Diaspora.Modules.Recommendations.Domain.Candidates;
using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.ColdStart;
using Diaspora.Modules.Recommendations.Extensibility.Personalization;
using Diaspora.Modules.Recommendations.Infrastructure.Ranking;

namespace Diaspora.Modules.Recommendations.Infrastructure.ColdStart;

/// <summary>
/// The first page for somebody the platform has never seen.
/// <para>
/// <b>Country first, and everything else follows from it.</b> The one thing known about a member
/// ninety seconds after registration is where they are, and that single fact narrows the platform's
/// whole corpus to the part that could possibly be useful. The ladder — guides, then housing, jobs,
/// organizations, events, providers, marketplace — is the order somebody who has just moved needs
/// things in.
/// </para>
/// <para>
/// <b>It widens rather than failing when a country has nothing.</b> A member in a country the
/// platform launched in last week would get an empty page from a country-only query, which is the
/// worst possible first impression and is avoidable: the fallback pass drops the country filter and
/// takes what is globally most engaged. An empty feed for a new member is the one outcome worth
/// spending a second query to avoid.
/// </para>
/// <para>
/// <b>Quotas per rung rather than "the best N overall".</b> Taking the top twenty by popularity
/// would return twenty guides, because guides accumulate engagement over years and a job posted
/// yesterday cannot compete. The whole point of the ladder is that a new arrival sees one of each
/// kind of thing the platform does.
/// </para>
/// </summary>
public sealed class LocationLadderColdStartProvider : IColdStartProvider
{
    public const string ProviderKey = "location-ladder";

    private readonly IRecommendationCandidateRepository _candidates;

    public LocationLadderColdStartProvider(IRecommendationCandidateRepository candidates) =>
        _candidates = candidates;

    public string Key => ProviderKey;

    public async Task<IReadOnlyList<RecommendationCandidate>> BuildAsync(
        PersonalizationContext context, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var ladder = ResolveLadder(context);
        var country = context.EffectiveCountryCode;

        // At least one of everything, and the remainder spread across the top of the ladder.
        var quota = Math.Max(1, (int)Math.Ceiling((double)limit / ladder.Count));

        var selected = new List<RecommendationCandidate>(limit);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var rung in ladder)
        {
            if (selected.Count >= limit)
            {
                break;
            }

            var forRung = await _candidates.ListMostPopularAsync(
                [rung], country, quota, context.AsOf, cancellationToken);

            foreach (var candidate in forRung)
            {
                if (seen.Add(candidate.Target.Key))
                {
                    selected.Add(candidate);
                }
            }
        }

        if (selected.Count >= limit || country is null)
        {
            return selected;
        }

        // The country has less than a page. Widen. See the class summary.
        var global = await _candidates.ListMostPopularAsync(
            ladder, countryCode: null, limit - selected.Count, context.AsOf, cancellationToken);

        foreach (var candidate in global)
        {
            if (selected.Count >= limit)
            {
                break;
            }

            if (seen.Add(candidate.Target.Key))
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    /// <summary>
    /// The ladder for this request.
    /// <para>
    /// A surface that asked for one kind of thing gets a ladder of one — a cold-start jobs page is
    /// still a jobs page, and returning guides on it because the member is new would be answering a
    /// question nobody asked.
    /// </para>
    /// </summary>
    private static List<RecommendationTargetType> ResolveLadder(PersonalizationContext context) =>
        context.TargetType is { } targetType
            ? [targetType]
            : [.. ColdStartRankingStrategy.DefaultLadder];
}
