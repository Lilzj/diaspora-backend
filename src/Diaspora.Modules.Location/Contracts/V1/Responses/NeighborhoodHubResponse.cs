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

    /// <summary>
    /// The outline of the nearest place above this one that has an outline — the town or district
    /// the neighbourhood sits in.
    /// <para>
    /// <b>No open dataset draws our neighbourhoods.</b> London's are electoral wards, Manchester's
    /// are a ward and an administrative district, and Mauritius's are stations and localities; none
    /// of them is the place a member means. A neighbourhood therefore keeps its approximate circle,
    /// and this is the real line a map can draw beside it — labelled as the town's, never as the
    /// neighbourhood's, which is why it is a separate field with a name of its own on it rather than
    /// something to fall back into <see cref="Boundary"/>.
    /// </para>
    /// <para>Null when no ancestor has an outline either.</para>
    /// </summary>
    public ContainingOutlineResponse? ContainingOutline { get; init; }
}

/// <summary>
/// Somebody else's outline: the line of the town or district a place sits in, with the name and
/// level that say whose line it is. A client draws it under that name and shows the credit in
/// <see cref="Boundary"/>.
/// </summary>
public sealed record ContainingOutlineResponse
{
    public required Guid AreaId { get; init; }

    public required string Name { get; init; }

    /// <summary><c>City</c>, <c>District</c>, <c>Region</c>, <c>StateProvince</c>.</summary>
    public required string Level { get; init; }

    public required GeographicBoundaryResponse Boundary { get; init; }
}
