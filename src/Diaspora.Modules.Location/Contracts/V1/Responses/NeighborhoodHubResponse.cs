namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// The neighbourhood hub screen's payload.
/// <para>
/// Deliberately field-compatible with the frontend's <c>NeighborhoodOverview</c>, which the
/// hub screen binds today: <c>id</c>, <c>name</c>, <c>cityName</c>, <c>countryName</c>,
/// <c>countryCode</c>, <c>latitude</c>, <c>longitude</c>, <c>overview</c>, <c>stats</c> and
/// <c>resourceCounts</c> all appear here under the same names. The two additions are
/// <c>slug</c> and <c>areaId</c>, and both are additive.
/// </para>
/// <para>
/// <c>resourceCounts</c> is a dictionary rather than the mock's fixed six keys, because the
/// six are whichever verticals have registered a counter. A vertical that ships later appears
/// without a contract change; one that has not shipped is absent rather than reported as zero.
/// </para>
/// </summary>
public sealed record NeighborhoodHubResponse
{
    /// <summary>The slug, which is what the frontend's route already carries as <c>id</c>.</summary>
    public required string Id { get; init; }

    public required Guid AreaId { get; init; }

    public required string Slug { get; init; }

    public required string Name { get; init; }

    public string? CityName { get; init; }

    public required string CountryName { get; init; }

    public required string CountryCode { get; init; }

    public required string Flag { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Prose about the area. Null when none has been written.</summary>
    public string? Overview { get; init; }

    /// <summary>
    /// The neighbourhood's outline, when the catalogue holds one. Null is "no outline imported",
    /// and a map should fall back to the centre pin rather than drawing a guess.
    /// </summary>
    public GeographicBoundaryResponse? Boundary { get; init; }

    public required NeighborhoodStatsResponse Stats { get; init; }

    /// <summary>Contributed counts, keyed by resource type.</summary>
    public required IReadOnlyDictionary<string, int> ResourceCounts { get; init; }

    /// <summary>Ancestors root-first, for the hub's breadcrumb.</summary>
    public required IReadOnlyList<AreaSummaryResponse> Ancestors { get; init; }
}
