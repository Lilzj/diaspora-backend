using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Extensibility.Ranking;

namespace Diaspora.Modules.Housing.Infrastructure.Ranking;

/// <summary>
/// Recency, verification and availability together.
/// <para>
/// <b>What a member actually wants, which is none of the three on its own.</b> Newest alone
/// surfaces whatever was posted an hour ago regardless of whether anybody has checked it; verified
/// alone buries every listing from an honest landlord who has not bothered; soonest alone ignores
/// that a property available today may have been sitting unlet for a reason.
/// </para>
/// <para>
/// <b>A score rather than a chain of tie-breaks, and the weights are stated rather than tuned.</b>
/// Verification is worth most because it is the thing a member cannot check for themselves from
/// another country. Availability is worth next most because a property they cannot move into is
/// not a result. Recency is worth least because it is the weakest signal about whether a home is
/// any good — it is only a signal about whether it is still there.
/// </para>
/// <para>
/// This is a starting point, not a claim to be right. It is a contributed strategy precisely so a
/// deployment that learns better can ship its own without touching the module.
/// </para>
/// </summary>
public sealed class BalancedRanking : IHousingRankingStrategy
{
    private const double VerifiedWeight = 3d;
    private const double AvailableWeight = 2d;
    private const double RecencyWeight = 1d;

    /// <summary>
    /// How quickly recency decays. Fourteen days: a listing a fortnight old scores about half what
    /// one posted today does, which matches how fast good housing actually goes.
    /// </summary>
    private const double RecencyHalfLifeDays = 14d;

    public string Key => HousingRankingKeys.Balanced;

    public IReadOnlyList<HousingListing> Rank(
        IReadOnlyList<HousingListing> listings, HousingRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(listings);
        ArgumentNullException.ThrowIfNull(context);

        return
        [
            .. listings
                .OrderByDescending(listing => Score(listing, context))
                .ThenBy(listing => listing.Slug.Value, StringComparer.Ordinal)
        ];
    }

    private static double Score(HousingListing listing, HousingRankingContext context)
    {
        var score = 0d;

        if (listing.TrustMark.IsVerified)
        {
            score += VerifiedWeight;
        }

        if (listing.Availability.IsAvailableOn(context.Now)
            && listing.Availability.HasUnitsRemaining)
        {
            score += AvailableWeight;
        }

        var published = listing.PublishedAt ?? listing.CreatedAt;
        var ageDays = Math.Max(0d, (context.Now - published).TotalDays);

        // Exponential decay rather than a cliff: a listing does not stop being relevant on its
        // fifteenth day, it just matters a little less than it did on its first.
        score += RecencyWeight * Math.Pow(0.5d, ageDays / RecencyHalfLifeDays);

        return score;
    }
}
