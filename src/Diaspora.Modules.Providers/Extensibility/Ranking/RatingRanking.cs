using Diaspora.Modules.Providers.Domain.Providers;

namespace Diaspora.Modules.Providers.Extensibility.Ranking;

/// <summary>
/// Best rated first. The client's "Rating" sort.
/// <para>
/// <b>Not a raw average sort, and the difference is the whole point.</b> The client's own
/// implementation is <c>b.rating.average - a.rating.average</c>, which puts a provider with one
/// five-star review above a provider with two hundred reviews averaging 4.8. That is not what a
/// member choosing a solicitor means by "sort by rating", and it is trivially gameable by
/// anybody willing to ask one friend.
/// </para>
/// <para>
/// The Wilson lower bound answers the right question: given the reviews so far, what is the
/// rating we can be confident this provider is <em>at least</em>? Twenty reviews averaging 4.5
/// beat one review at 5.0, because the evidence supports it. It is the same estimator Reddit
/// uses for comment ranking, and it needs no tuning parameter beyond the confidence level.
/// </para>
/// </summary>
public sealed class RatingRanking : IProviderRankingStrategy
{
    /// <summary>The z-score for a 95% one-sided confidence bound.</summary>
    private const double ConfidenceZ = 1.96d;

    public string Key => RankingKeys.Rating;

    public string Module => ProvidersModuleMetadata.Name;

    public IReadOnlyList<Provider> Rank(
        IReadOnlyList<Provider> candidates, ProviderRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        return candidates
            .OrderByDescending(LowerBound)
            .ThenByDescending(provider => provider.ReviewSummary.TotalReviews)
            .ThenBy(provider => provider.Id.Value)
            .ToList();
    }

    /// <summary>
    /// The Wilson score lower bound, over the five-point scale mapped to [0, 1].
    /// <para>
    /// A provider with no reviews scores zero rather than the prior mean, which is correct for
    /// <em>this</em> sort specifically: a member who explicitly asked to sort by rating is asking
    /// to see rated providers, and putting unrated ones among them answers a question nobody
    /// asked. The balanced sort, which is the default, treats them far more generously.
    /// </para>
    /// </summary>
    private static double LowerBound(Provider provider)
    {
        var count = provider.ReviewSummary.TotalReviews;

        if (count == 0)
        {
            return 0d;
        }

        // The average on 1..5 mapped to a proportion on 0..1, so the binomial form applies.
        var proportion = (provider.ReviewSummary.Average - 1d) / 4d;

        var zSquared = ConfidenceZ * ConfidenceZ;
        var denominator = 1d + (zSquared / count);

        var centre = proportion + (zSquared / (2d * count));

        var margin = ConfidenceZ * Math.Sqrt(
            ((proportion * (1d - proportion)) + (zSquared / (4d * count))) / count);

        return (centre - margin) / denominator;
    }
}
