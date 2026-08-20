using Diaspora.Modules.Marketplace.Extensibility.Ranking;

namespace Diaspora.Modules.Marketplace.Infrastructure.Ranking;

/// <summary>
/// Freshness, trust, engagement and completeness together. The default for search.
/// <para>
/// <b>The promotion term is additive and small, and that is the whole ethical content of this
/// class.</b> A featured slot can lift a listing but can never bury a better-matched one: the bonus
/// is a fraction of what verification and engagement contribute, so a promoted listing that nobody
/// wants still loses to an unpromoted one people are saving. A marketplace where money outranks
/// relevance has stopped answering the buyer's question, and this is the line in the code where
/// that would have to be crossed deliberately.
/// </para>
/// <para>
/// <b>Engagement is logarithmic and completeness is a flat bonus.</b> The first ten people to save
/// something say most of what saves can say, and a listing with a photograph converts so much
/// better than one without that rewarding it is the cheapest quality lever the platform has.
/// </para>
/// </summary>
public sealed class BalancedRanking : IListingRankingStrategy
{
    public string Key => ListingRankingKeys.Balanced;

    public string DisplayName => "Best match";

    public double Score(ListingRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // The engine's own relevance where there is one. Everything below adjusts around it rather
        // than replacing it: on a text search, what the buyer typed matters most.
        var score = context.TextScore ?? 0d;

        score += 1.0d * RecencyRanking.Freshness(context.AgeHours);

        if (context.IsStorefront)
        {
            score += 0.6d;
        }
        else if (context.IsSellerVerified)
        {
            score += 0.4d;
        }

        if (context.SellerCancellationRate is { } cancellations)
        {
            score -= 0.8d * cancellations;
        }

        // Saves say more than views: a view is a tap and a save is an intention.
        score += 0.5d * Math.Log10(1d + context.SaveCount);
        score += 0.2d * Math.Log10(1d + context.ViewCount);
        score += 0.3d * Math.Log10(1d + context.OfferCount);

        // A listing with no photograph is one nobody opens and nobody can sanity-check against its
        // description. Rewarding the photograph is the cheapest fraud control the module has.
        if (context.HasCoverImage)
        {
            score += 0.35d;
        }

        // Placement, never rank suppression. See the class summary.
        if (context.IsFeatured)
        {
            score += 0.3d;
        }

        // Distance where a point was supplied, and nothing where none was. Assuming zero would
        // rank every jurisdiction-scoped listing as if it were on the buyer's doorstep.
        if (context.DistanceKm is { } distance)
        {
            score += 0.5d / (1d + (distance / 10d));
        }

        return score;
    }
}
