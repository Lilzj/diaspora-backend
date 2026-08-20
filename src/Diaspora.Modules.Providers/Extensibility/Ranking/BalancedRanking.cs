using Diaspora.Modules.Providers.Domain.Providers;

namespace Diaspora.Modules.Providers.Extensibility.Ranking;

/// <summary>
/// The default ordering behind the client's "Recommended".
/// <para>
/// <b>Deliberately simple and explicable.</b> Every term is something a provider can influence
/// and a support agent can explain, which is the property that matters most for a directory
/// people's livelihoods depend on. "Why am I below them?" has an answer here — you are further
/// away, or you have fewer reviews, or your profile is half finished — and that answer is
/// actionable. A learned ranker gives a better order and no answer at all, which is why one is
/// B17's job and not B7's.
/// </para>
/// <para>
/// <b>Rating is damped by review count</b>, using a Bayesian prior against the platform mean.
/// Without it a single five-star review from the owner's cousin outranks a hundred reviews
/// averaging 4.6, which is both wrong and the easiest thing in the system to game.
/// </para>
/// <para>
/// <b>Distance is scored as a decay, not a rank.</b> Sorting by distance is what the "Distance"
/// option is for; here, being twice as far should cost something without being decisive, because
/// a member searching "Recommended" has said they care about more than proximity. Beyond about
/// twenty-five kilometres the term flattens, since the difference between forty and fifty
/// kilometres is not a difference anybody feels.
/// </para>
/// <para>
/// <b>Featured placement is a tie-breaker, not a multiplier.</b> It moves a provider up among
/// its near-equals and cannot lift a poor one above a good one. A featured slot that outranked
/// quality would be an advertising product, and the brief did not ask for one.
/// </para>
/// </summary>
public sealed class BalancedRanking : IProviderRankingStrategy
{
    /// <summary>
    /// The prior weight in the Bayesian damping: a provider is treated as starting with this
    /// many reviews at the platform mean. Five is enough that one or two reviews barely move
    /// the score and ten reviews mostly own it.
    /// </summary>
    private const double PriorReviewWeight = 5d;

    /// <summary>
    /// The assumed platform mean rating. Ratings on a directory of local businesses cluster
    /// high; 4.0 is a deliberately conservative prior, so a new provider is not flattered.
    /// </summary>
    private const double PriorMeanRating = 4.0d;

    /// <summary>Where the distance term has decayed to half. Roughly a city's radius.</summary>
    private const double DistanceHalfLifeKm = 8d;

    public string Key => RankingKeys.Balanced;

    public string Module => ProvidersModuleMetadata.Name;

    public IReadOnlyList<Provider> Rank(
        IReadOnlyList<Provider> candidates, ProviderRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(context);

        return candidates
            .OrderByDescending(provider => Score(provider, context))

            // A stable final tie-break so two providers with identical scores do not swap
            // between pages of the same result set — which is how a member sees the same
            // business twice and misses another entirely.
            .ThenBy(provider => provider.Id.Value)
            .ToList();
    }

    private static double Score(Provider provider, ProviderRankingContext context)
    {
        var rating = DampedRating(provider);
        var proximity = Proximity(provider, context);
        var verification = provider.IsVerified ? 1d : 0d;
        var completeness = provider.Statistics.ProfileCompletionPercent / 100d;
        var engagement = Engagement(provider);
        var community = CommunityMatch(provider, context);

        // Weights sum to one, so the score is always in [0, 1] and is comparable across
        // deployments. Rating leads because it is what members actually asked about; proximity
        // is close behind because this is a hyperlocal product.
        var score =
            (0.30d * rating)
            + (0.25d * proximity)
            + (0.15d * verification)
            + (0.12d * completeness)
            + (0.10d * engagement)
            + (0.08d * community);

        // Featured is a nudge among near-equals, not a multiplier that reorders the page.
        return provider.IsFeatured ? score + 0.02d : score;
    }

    /// <summary>
    /// The Bayesian-damped rating, normalised to [0, 1].
    /// <para>
    /// <c>(v·R + m·C) / (v + m)</c>, the standard weighted-rating form: a provider with no
    /// reviews scores the prior mean, and its own average takes over as evidence accumulates.
    /// </para>
    /// </summary>
    private static double DampedRating(Provider provider)
    {
        var count = provider.ReviewSummary.TotalReviews;
        var average = provider.ReviewSummary.Average;

        var damped =
            ((count * average) + (PriorReviewWeight * PriorMeanRating))
            / (count + PriorReviewWeight);

        return Math.Clamp((damped - 1d) / 4d, 0d, 1d);
    }

    /// <summary>
    /// Exponential decay on distance, so nearer is better without being decisive.
    /// <para>
    /// A provider with no distance — because the scope was not spatial, or because it serves
    /// remotely — scores the neutral midpoint rather than zero. Scoring it zero would bury every
    /// remote provider in every location-aware search, which is precisely the bug the delivery
    /// mode exists to prevent.
    /// </para>
    /// </summary>
    private static double Proximity(Provider provider, ProviderRankingContext context)
    {
        if (context.DistancesKm.Count == 0)
        {
            return 0.5d;
        }

        if (!context.DistancesKm.TryGetValue(provider.Id.Value, out var distanceKm))
        {
            return provider.ServesRemotely ? 0.6d : 0.4d;
        }

        return Math.Exp(-distanceKm / DistanceHalfLifeKm);
    }

    /// <summary>
    /// How much the community has engaged, on a logarithmic curve.
    /// <para>
    /// Logarithmic because the difference between two reviews and twenty matters far more than
    /// between two hundred and two hundred and twenty, and a linear term would let one
    /// established provider dominate every page in its category forever.
    /// </para>
    /// </summary>
    private static double Engagement(Provider provider)
    {
        var signals =
            provider.ReviewSummary.TotalReviews
            + provider.Statistics.RecommendationCount
            + (provider.Statistics.BookmarkCount / 4d);

        return Math.Clamp(Math.Log10(signals + 1d) / 2.5d, 0d, 1d);
    }

    /// <summary>
    /// Whether the provider is in the viewer's own country community.
    /// <para>
    /// The smallest weight in the score, and present because this is a diaspora platform: a
    /// Ghanaian member in London looking for a caterer is, more often than not, looking for a
    /// Ghanaian caterer. Small, because assuming it too strongly would wall members into their
    /// own community, which is the opposite of what the product is for.
    /// </para>
    /// </summary>
    private static double CommunityMatch(Provider provider, ProviderRankingContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ViewerCountryCode))
        {
            return 0.5d;
        }

        return string.Equals(
            provider.Location.CountryCode,
            context.ViewerCountryCode,
            StringComparison.OrdinalIgnoreCase)
            ? 1d
            : 0.5d;
    }
}
