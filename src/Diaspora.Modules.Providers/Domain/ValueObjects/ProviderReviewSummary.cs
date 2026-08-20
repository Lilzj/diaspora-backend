using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Providers.Domain.ValueObjects;

/// <summary>
/// The rating rollup a provider card renders without touching the reviews table.
/// <para>
/// The client's <c>rating: { average, totalReviews, distribution }</c> exactly, and the
/// distribution is not optional padding — the provider detail screen's
/// <c>RatingDisplay</c> draws five proportional bars from it, and computing them client-side
/// would mean shipping every review to draw a summary.
/// </para>
/// <para>
/// <b>Recomputed, never incremented.</b> A summary maintained by <c>+1</c> on write drifts the
/// first time a review is hidden, edited from four stars to two, or removed after a moderation
/// appeal — and it drifts silently, because nothing compares it to the truth. The five bucket
/// counts are the state; the average and the total are derived from them on construction. That
/// makes the whole summary reconstructible from a single <c>GROUP BY rating</c>, which is
/// exactly what the repair path does.
/// </para>
/// <para>
/// <see cref="Average"/> is rounded to one decimal because that is what every surface renders
/// (<c>rating.toFixed(1)</c> in three client components) and storing more precision than
/// anything displays invites two screens to disagree over 4.849999.
/// </para>
/// </summary>
public sealed class ProviderReviewSummary : ValueObject
{
    public const int MinRating = 1;

    public const int MaxRating = 5;

    private ProviderReviewSummary(
        int oneStar, int twoStar, int threeStar, int fourStar, int fiveStar)
    {
        OneStar = oneStar;
        TwoStar = twoStar;
        ThreeStar = threeStar;
        FourStar = fourStar;
        FiveStar = fiveStar;

        TotalReviews = oneStar + twoStar + threeStar + fourStar + fiveStar;

        var weighted = oneStar + (2 * twoStar) + (3 * threeStar) + (4 * fourStar) + (5 * fiveStar);

        Average = TotalReviews == 0
            ? 0d
            : Math.Round(weighted / (double)TotalReviews, 1, MidpointRounding.AwayFromZero);
    }

    private ProviderReviewSummary() { }

    public int OneStar { get; private set; }

    public int TwoStar { get; private set; }

    public int ThreeStar { get; private set; }

    public int FourStar { get; private set; }

    public int FiveStar { get; private set; }

    /// <summary>Derived. The client's <c>rating.totalReviews</c>.</summary>
    public int TotalReviews { get; private set; }

    /// <summary>Derived, one decimal place. The client's <c>rating.average</c>.</summary>
    public double Average { get; private set; }

    public bool HasReviews => TotalReviews > 0;

    public static ProviderReviewSummary Empty => new(0, 0, 0, 0, 0);

    /// <summary>
    /// Builds a summary from bucket counts. The only constructor, because every other shape —
    /// an average and a total, say — cannot reproduce the distribution the client draws.
    /// </summary>
    public static ProviderReviewSummary FromDistribution(
        int oneStar, int twoStar, int threeStar, int fourStar, int fiveStar) =>
        new(
            Math.Max(0, oneStar),
            Math.Max(0, twoStar),
            Math.Max(0, threeStar),
            Math.Max(0, fourStar),
            Math.Max(0, fiveStar));

    /// <summary>Builds a summary from raw ratings. The repair path's entry point.</summary>
    public static ProviderReviewSummary FromRatings(IEnumerable<int> ratings)
    {
        ArgumentNullException.ThrowIfNull(ratings);

        var buckets = new int[MaxRating + 1];

        foreach (var rating in ratings)
        {
            if (rating is >= MinRating and <= MaxRating)
            {
                buckets[rating]++;
            }
        }

        return new ProviderReviewSummary(buckets[1], buckets[2], buckets[3], buckets[4], buckets[5]);
    }

    /// <summary>The count in one bucket, for a caller that has the star as a number.</summary>
    public int CountFor(int rating) => rating switch
    {
        1 => OneStar,
        2 => TwoStar,
        3 => ThreeStar,
        4 => FourStar,
        5 => FiveStar,
        _ => 0
    };

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return OneStar;
        yield return TwoStar;
        yield return ThreeStar;
        yield return FourStar;
        yield return FiveStar;
    }
}
