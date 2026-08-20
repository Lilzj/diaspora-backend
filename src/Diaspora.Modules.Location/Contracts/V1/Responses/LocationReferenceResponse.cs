namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// The one way the platform describes a place.
/// <para>
/// <b>The most important DTO in this module.</b> The frontend currently declares seven
/// near-identical location shapes — <c>HousingLocation</c>, <c>JobLocation</c>,
/// <c>MarketplaceLocation</c>, <c>EventLocation</c>, <c>UpdateLocation</c>, the guide's inline
/// <c>location</c>, and the provider's — four of which are field-for-field identical and the
/// rest of which differ only in which fields are optional. Every future vertical embeds this
/// instead, and the duplication stops here.
/// </para>
/// <para>
/// It is a strict superset of the frontend's <c>PlatformLocation</c>, so every existing
/// client binding keeps working unchanged. What it adds is identifiers: flat name strings
/// cannot be joined, cached or disambiguated, and there are twelve Springfields. Recorded as
/// intentional deviation #3 and #5 in the frontend contract report.
/// </para>
/// </summary>
public sealed record LocationReferenceResponse
{
    /// <summary>ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    public required string CountryName { get; init; }

    /// <summary>Emoji, derived from the country code.</summary>
    public required string Flag { get; init; }

    /// <summary>The first-level subdivision. The frontend's <c>state</c>.</summary>
    public string? State { get; init; }

    public string? CityName { get; init; }

    /// <summary>The frontend's <c>neighborhood</c>.</summary>
    public string? Neighborhood { get; init; }

    /// <summary>
    /// The same value as <see cref="Neighborhood"/>, under the name five of the frontend's
    /// seven location DTOs use for it. Emitted twice rather than picking a winner, because
    /// picking one would break the other five for no gain. See naming finding #15.
    /// </summary>
    public string? AreaName { get; init; }

    public string? Street { get; init; }

    public string? AddressLine { get; init; }

    public string? Landmark { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>"Peckham, London".</summary>
    public required string DisplayName { get; init; }

    /// <summary>The catalogue node, when this reference resolved to one.</summary>
    public Guid? AreaId { get; init; }

    /// <summary>The containing city's node. Matches the provider DTO's existing <c>cityId</c>.</summary>
    public Guid? CityId { get; init; }

    public Guid? CountryId { get; init; }

    /// <summary>The URL-safe name. What the neighbourhood hub route resolves.</summary>
    public string? Slug { get; init; }

    /// <summary>The node's rung: <c>City</c>, <c>Neighborhood</c>, and so on.</summary>
    public string? Level { get; init; }

    /// <summary>
    /// Distance from whatever the query centred on. Present only on proximity results — the
    /// field the frontend renders through <c>DistanceBadge</c> and <c>NearbyIndicator</c>.
    /// <para>
    /// On a reverse-geocode this is the distance from the coordinate that was asked about to the
    /// place that answered, which is the only honest measure of how good the answer is.
    /// </para>
    /// </summary>
    public double? DistanceKm { get; init; }

    /// <summary>
    /// How well a resolved place matches the coordinate it was resolved from: <c>Exact</c>,
    /// <c>High</c>, <c>Medium</c> or <c>Low</c>. Null when the reference did not come from a
    /// coordinate.
    /// <para>
    /// <b>Why the server says this rather than the client deriving it.</b> Reverse geocoding
    /// against the catalogue answers with the nearest node, and the nearest node to somebody
    /// standing on a farm forty kilometres from anywhere is a town they are not in. Without a
    /// stated confidence every client has to re-derive the distance and pick its own threshold,
    /// and four clients pick four thresholds — one of which will put a member in the wrong
    /// country on a form they then sign.
    /// </para>
    /// </summary>
    public string? Confidence { get; init; }
}
