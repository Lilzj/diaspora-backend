using Diaspora.Modules.Marketplace.Extensibility.Ranking;

namespace Diaspora.Modules.Marketplace.Infrastructure.Ranking;

/// <summary>
/// Newest first, and nothing else considered.
/// <para>
/// The baseline every other strategy is compared against, and the right answer more often than a
/// marketplace's operators expect: on a board where most listings are one-off second-hand items,
/// what is new <em>is</em> what is relevant, and anything cleverer mostly reorders noise.
/// </para>
/// </summary>
public sealed class RecencyRanking : IListingRankingStrategy
{
    public string Key => ListingRankingKeys.Recency;

    public string DisplayName => "Newest first";

    /// <summary>
    /// A decay curve rather than a raw age, so the numbers stay comparable with the other
    /// strategies' and stay bounded between zero and one however old a listing gets.
    /// </summary>
    public double Score(ListingRankingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Freshness(context.AgeHours);
    }

    /// <summary>
    /// One at publication, halving every week. Shared with the other strategies so "fresh" means
    /// the same thing whichever ranking a deployment picks.
    /// </summary>
    internal static double Freshness(double ageHours) =>
        Math.Pow(0.5d, Math.Max(0d, ageHours) / 168d);
}
