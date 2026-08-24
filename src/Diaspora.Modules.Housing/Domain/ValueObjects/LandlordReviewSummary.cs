using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.ValueObjects;

/// <summary>
/// The rating rollup a housing card renders without touching the reviews table.
/// <para>
/// The same shape as B10's <c>SellerReviewSummary</c> and B7's <c>ProviderReviewSummary</c>, so
/// one client component draws all three.
/// </para>
/// <para>
/// <b>Recomputed, never incremented.</b> A summary maintained by <c>+1</c> on write drifts the
/// first time a review is hidden, edited from four stars to two, or withdrawn — and it drifts
/// silently, because nothing compares it to the truth. The five bucket counts are the state, so
/// the whole summary is reconstructible from a single <c>GROUP BY rating</c>.
/// </para>
/// </summary>
public sealed class LandlordReviewSummary : ValueObject
{
    public const int MinRating = 1;

    public const int MaxRating = 5;

    private LandlordReviewSummary(
        int oneStar, int twoStar, int threeStar, int fourStar, int fiveStar)
    {
        OneStar = oneStar;
        TwoStar = twoStar;
        ThreeStar = threeStar;
        FourStar = fourStar;
        FiveStar = fiveStar;
    }

    private LandlordReviewSummary() { }

    public int OneStar { get; private set; }

    public int TwoStar { get; private set; }

    public int ThreeStar { get; private set; }

    public int FourStar { get; private set; }

    public int FiveStar { get; private set; }

    /// <summary>
    /// Derived on read, not stored.
    /// <para>
    /// <b>Computed rather than assigned in the constructor, and that is not a style choice.</b>
    /// EF materialises an owned type through its parameterless constructor and then sets the
    /// mapped columns — it never runs the constructor that did the arithmetic. B10's first
    /// version of this assigned the total on construction, persisted its buckets correctly, and
    /// then served <c>totalReviews: 0</c> beside them for every seller loaded from the database.
    /// </para>
    /// </summary>
    public int TotalReviews => OneStar + TwoStar + ThreeStar + FourStar + FiveStar;

    /// <summary>
    /// Derived, one decimal place.
    /// <para>
    /// <b>Zero when there are no reviews, and a reader must not render that as a rating.</b>
    /// The housing screen rendered <c>★ {listing.host.rating}</c> against a field nothing sent;
    /// an unrated landlord and a nought-star one are different facts, and
    /// <see cref="HasReviews"/> is how a caller tells them apart.
    /// </para>
    /// </summary>
    public double Average
    {
        get
        {
            var total = TotalReviews;

            if (total == 0)
            {
                return 0d;
            }

            var weighted =
                OneStar + (2 * TwoStar) + (3 * ThreeStar) + (4 * FourStar) + (5 * FiveStar);

            return Math.Round(weighted / (double)total, 1, MidpointRounding.AwayFromZero);
        }
    }

    public bool HasReviews => TotalReviews > 0;

    public static LandlordReviewSummary Empty => new(0, 0, 0, 0, 0);

    /// <summary>
    /// Builds a summary from bucket counts. The only constructor, because every other shape —
    /// an average and a total, say — cannot reproduce the distribution the client draws.
    /// </summary>
    public static LandlordReviewSummary FromDistribution(
        int oneStar, int twoStar, int threeStar, int fourStar, int fiveStar) =>
        new(
            Math.Max(0, oneStar),
            Math.Max(0, twoStar),
            Math.Max(0, threeStar),
            Math.Max(0, fourStar),
            Math.Max(0, fiveStar));

    /// <summary>Builds a summary from raw ratings. The repair path's entry point.</summary>
    public static LandlordReviewSummary FromRatings(IEnumerable<int> ratings)
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

        return new LandlordReviewSummary(
            buckets[1], buckets[2], buckets[3], buckets[4], buckets[5]);
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
