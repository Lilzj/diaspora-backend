namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// How a result page is ordered.
/// <para>
/// <see cref="Relevance"/> and <see cref="Nearest"/> degrade to <see cref="Newest"/> in the
/// shipped EF engine, which has neither a score nor geometry. A real engine implements both
/// behind <c>IHousingSearchEngine</c> and nothing above the port changes.
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
    Verified = 7
}
