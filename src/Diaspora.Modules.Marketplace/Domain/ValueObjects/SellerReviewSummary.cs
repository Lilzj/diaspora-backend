using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.ValueObjects;

/// <summary>
/// The rating rollup a listing card renders without touching the reviews table.
/// <para>
/// Deliberately the same shape as B7's <c>ProviderReviewSummary</c>. The client already draws
/// proportional bars from <c>rating: { average, totalReviews, distribution }</c> on the provider
/// screen, and a seller rating that arrived in a different shape would need a second component
/// to render the same picture.
/// </para>
/// <para>
/// <b>Recomputed, never incremented.</b> A summary maintained by <c>+1</c> on write drifts the
/// first time a review is hidden, edited from four stars to two, or withdrawn — and it drifts
/// silently, because nothing compares it to the truth. The five bucket counts are the state;
/// the average and total are derived on construction, so the whole summary is reconstructible
/// from a single <c>GROUP BY rating</c>.
/// </para>
/// <para>
/// <see cref="Average"/> is rounded to one decimal because that is what the surfaces render.
/// Storing more precision than anything displays invites two screens to disagree over 4.849999.
/// </para>
/// </summary>
public sealed class SellerReviewSummary : ValueObject
{
    public const int MinRating = 1;

    public const int MaxRating = 5;

    private SellerReviewSummary(int oneStar, int twoStar, int threeStar, int fourStar, int fiveStar)
    {
        OneStar = oneStar;
        TwoStar = twoStar;
        ThreeStar = threeStar;
        FourStar = fourStar;
        FiveStar = fiveStar;
    }

    private SellerReviewSummary() { }

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
    /// mapped columns — it never runs the constructor that did the arithmetic. A version of this
    /// that assigned the total on construction persisted its buckets correctly and then served
    /// <c>totalReviews: 0</c> beside them for every seller loaded from the database.
    /// </para>
    /// <para>
    /// B7's <c>ProviderReviewSummary</c> stores both instead, for a reason that does not apply
    /// here yet: its discovery surface sorts and filters on rating and wants an index. When
    /// marketplace discovery needs the same, add the columns then — and note that a stored
    /// derived value is a second place for the number to be wrong.
    /// </para>
    /// </summary>
    public int TotalReviews => OneStar + TwoStar + ThreeStar + FourStar + FiveStar;

    /// <summary>
    /// Derived, one decimal place.
    /// <para>
    /// <b>Zero when there are no reviews, and a reader must not render that as a rating.</b>
    /// This is the field the marketplace screen used to substitute <c>4.8</c> for when it was
    /// missing; a nought-star seller and an unrated seller are different facts, and
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

    public static SellerReviewSummary Empty => new(0, 0, 0, 0, 0);

    /// <summary>
    /// Builds a summary from bucket counts. The only constructor, because every other shape —
    /// an average and a total, say — cannot reproduce the distribution the client draws.
    /// </summary>
    public static SellerReviewSummary FromDistribution(
        int oneStar, int twoStar, int threeStar, int fourStar, int fiveStar) =>
        new(
            Math.Max(0, oneStar),
            Math.Max(0, twoStar),
            Math.Max(0, threeStar),
            Math.Max(0, fourStar),
            Math.Max(0, fiveStar));

    /// <summary>Builds a summary from raw ratings. The repair path's entry point.</summary>
    public static SellerReviewSummary FromRatings(IEnumerable<int> ratings)
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

        return new SellerReviewSummary(buckets[1], buckets[2], buckets[3], buckets[4], buckets[5]);
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
