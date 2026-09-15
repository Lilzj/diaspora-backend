namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// How a result page is ordered.
/// <para>
/// <see cref="Relevance"/> degrades to <see cref="Newest"/> in the shipped EF engine, which has no
/// score. <see cref="Nearest"/> is honoured for a radius search since 15 Sep 2026 — the engine
/// measures every home in the circle through Location and pages the sorted result — and degrades
/// to <see cref="Newest"/>, reported as the applied sort, with no point or too many homes.
/// </para>
/// </summary>
public enum HousingSortOrder
{
    Newest = 0,
    PriceLowToHigh = 1,
    PriceHighToLow = 2,
    MostViewed = 3,

    /// <summary>Ready to move into soonest. What somebody with a flight booked sorts by.</summary>
    AvailableSoonest = 4,

    Relevance = 5,
    Nearest = 6,

    /// <summary>Verified landlords first, then newest.</summary>
    Verified = 7,

    /// <summary>
    /// The landlord's average rating, highest first, then the number of reviews, then newest.
    /// Unrated landlords come last rather than as nought stars: no reviews is not a bad review.
    /// </summary>
    HighestRated = 8
}
