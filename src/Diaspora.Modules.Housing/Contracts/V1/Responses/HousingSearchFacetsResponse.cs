namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>
/// The counts beside the filters.
/// <para>
/// Not decoration: without them a member guesses which narrowing will empty the page, and on a
/// search where the good results vanish within hours an empty page is a member who gives up.
/// </para>
/// </summary>
public sealed record HousingSearchFacetsResponse
{
    public IReadOnlyDictionary<string, int> AccommodationTypes { get; init; } =
        new Dictionary<string, int>();

    public IReadOnlyDictionary<string, int> Amenities { get; init; } =
        new Dictionary<string, int>();

    public IReadOnlyDictionary<int, int> Bedrooms { get; init; } = new Dictionary<int, int>();

    /// <summary>Keyed by B4 area identifier, so a client narrows without a translation table.</summary>
    public IReadOnlyDictionary<Guid, int> Neighborhoods { get; init; } =
        new Dictionary<Guid, int>();

    public IReadOnlyDictionary<string, int> LeaseTypes { get; init; } =
        new Dictionary<string, int>();

    public IReadOnlyDictionary<string, int> FurnishedStatuses { get; init; } =
        new Dictionary<string, int>();

    /// <summary>One entry per currency and period pair present in the results.</summary>
    public IReadOnlyList<HousingPriceRangeFacetResponse> PriceRanges { get; init; } = [];

    public int VerifiedCount { get; init; }

    public int AccessibleCount { get; init; }

    public int ShortStayCount { get; init; }

    public int RoommateWantedCount { get; init; }
}
