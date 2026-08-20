using Diaspora.Modules.Marketplace.Extensibility.Ranking;

namespace Diaspora.Modules.Marketplace.Infrastructure.Ranking;

/// <summary>
/// Sellers a buyer can rely on first, then recency.
/// <para>
/// <b>For the markets where being scammed is the default expectation.</b> On a platform whose
/// members are buying across borders from strangers, a verified seller and a shop in the Providers
/// directory are worth more than a slightly better-matched listing from an account opened this
/// morning — and a deployment can say so by making this its default without changing a line of
/// search code.
/// </para>
/// <para>
/// <b>The cancellation rate subtracts.</b> A seller who abandons a third of their reservations is
/// wasting people's journeys, and no amount of verification makes up for it. It is the one signal
/// here that can push a listing down, and it is earned rather than paid for.
/// </para>
/// </summary>
public sealed class TrustedRanking : IListingRankingStrategy
{
    public string Key => ListingRankingKeys.Trusted;

    public string DisplayName => "Trusted sellers first";

    public double Score(ListingRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var trust = 0d;

        // A shop B7 has checked outranks a member Trust has checked, because the two answer
        // different questions and the shop's is the stronger one for a buyer handing over money.
        if (context.IsStorefront)
        {
            trust += 1.0d;
        }
        else if (context.IsSellerVerified)
        {
            trust += 0.7d;
        }

        // Completed sales, with diminishing returns. The tenth sale says far more than the
        // hundredth, and a linear term would let a high-volume shop bury everything else.
        trust += 0.3d * Math.Log10(1d + context.SellerCompletedSales);

        if (context.SellerCancellationRate is { } cancellations)
        {
            trust -= cancellations;
        }

        // Freshness is the tie-break, not the driver: among equally trustworthy sellers, newest.
        return trust + (0.25d * RecencyRanking.Freshness(context.AgeHours));
    }
}
