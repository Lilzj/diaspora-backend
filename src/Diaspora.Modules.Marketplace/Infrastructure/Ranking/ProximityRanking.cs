using Diaspora.Modules.Marketplace.Extensibility.Ranking;

namespace Diaspora.Modules.Marketplace.Infrastructure.Ranking;

/// <summary>
/// Closest first, degrading honestly where there is no distance to sort by.
/// <para>
/// <b>The strategy that matters most on a collection-only marketplace</b>, where the deciding
/// question is not what an item costs but whether the buyer can realistically go and get it.
/// </para>
/// <para>
/// <b>A listing with no distance is ranked by freshness rather than sunk.</b> Most listings on this
/// platform are jurisdiction-scoped — they have a country and a neighbourhood, not a coordinate —
/// so treating a missing distance as "infinitely far" would empty the results page of nearly
/// everything. It is treated as "unknown", which is what it is.
/// </para>
/// </summary>
public sealed class ProximityRanking : IListingRankingStrategy
{
    public string Key => ListingRankingKeys.Proximity;

    public string DisplayName => "Closest first";

    public double Score(ListingRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.DistanceKm is not { } distance)
        {
            // Unknown, not far. Scored below anything with a real distance inside about 25km and
            // above anything further, which is roughly where "worth the journey" sits.
            return 0.3d * RecencyRanking.Freshness(context.AgeHours);
        }

        // A hyperbolic falloff rather than a linear one: the difference between 1km and 3km matters
        // to somebody carrying a chair home, and the difference between 40km and 42km does not.
        var proximity = 1d / (1d + (Math.Max(0d, distance) / 5d));

        return proximity + (0.1d * RecencyRanking.Freshness(context.AgeHours));
    }
}
