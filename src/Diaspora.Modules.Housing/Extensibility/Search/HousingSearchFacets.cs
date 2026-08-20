namespace Diaspora.Modules.Housing.Extensibility.Search;

/// <summary>
/// The counts a search screen renders beside its filters.
/// <para>
/// <b>Facets are not decoration on a housing search.</b> Without them a member guesses which
/// narrowing will empty the page, and on a search where the good results vanish within hours an
/// empty page is a member who gives up. The counts say "eleven of these have parking" before they
/// tick the box.
/// </para>
/// <para>
/// Every map is keyed by the same value the filter takes, so a client renders a facet and sends it
/// back without a translation table. The neighbourhood map is keyed by B4's area identifier for
/// the same reason.
/// </para>
/// </summary>
public sealed record HousingSearchFacets
{
    /// <summary>Accommodation-type key to count.</summary>
    public IReadOnlyDictionary<string, int> AccommodationTypes { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Amenity key to count. Only amenities the catalogue marks filterable.</summary>
    public IReadOnlyDictionary<string, int> Amenities { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Bedroom count to number of properties. Zero is a studio, not a missing value.</summary>
    public IReadOnlyDictionary<int, int> Bedrooms { get; init; } = new Dictionary<int, int>();

    /// <summary>B4 area identifier to count. What a "narrow to a neighbourhood" list renders.</summary>
    public IReadOnlyDictionary<Guid, int> Neighborhoods { get; init; } =
        new Dictionary<Guid, int>();

    /// <summary>Lease-type name to count.</summary>
    public IReadOnlyDictionary<string, int> LeaseTypes { get; init; } =
        new Dictionary<string, int>();

    /// <summary>Furnished-status name to count.</summary>
    public IReadOnlyDictionary<string, int> FurnishedStatuses { get; init; } =
        new Dictionary<string, int>();

    /// <summary>
    /// The rent range present in the results, per currency and period.
    /// <para>
    /// Keyed by a currency-and-period pair because nothing in this module converts or normalises
    /// either, so there is no single range to report. A client renders the slider for whichever
    /// pair the member is filtering in.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, HousingPriceRangeFacet> PriceRanges { get; init; } =
        new Dictionary<string, HousingPriceRangeFacet>();

    /// <summary>Properties with a verified landlord.</summary>
    public int VerifiedCount { get; init; }

    /// <summary>Properties with at least one accessibility feature.</summary>
    public int AccessibleCount { get; init; }

    /// <summary>Properties accepting short stays. What somebody with six weeks before a move needs.</summary>
    public int ShortStayCount { get; init; }

    /// <summary>Shared properties actively looking for a roommate.</summary>
    public int RoommateWantedCount { get; init; }

    /// <summary>Nothing to report. What an engine returns when the caller did not ask.</summary>
    public static HousingSearchFacets Empty => new();
}
