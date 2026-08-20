namespace Diaspora.Modules.Providers.Contracts.V1.Requests;

/// <summary>
/// Everything the discovery endpoint accepts, bound from the query string.
/// <para>
/// <b>Every geographic field is optional and every combination is accepted</b>, because that is
/// the truth of the wire: the client's discovery tab sends city, latitude, longitude and radius
/// together; a deep link sends only a city; a map view sends only coordinates. All of it goes to
/// B4's <c>IGeographicScopeResolver</c> and is normalised once, which is the single most reusable
/// thing that module publishes.
/// </para>
/// <para>
/// The client's radius sentinels are honoured verbatim: a positive value is kilometres,
/// <c>-1</c> means "this city", <c>-2</c> means "this country". Those are B4's contract, not
/// B7's invention, and translating them here would be the second implementation of a rule that
/// already has one.
/// </para>
/// </summary>
public sealed record ProviderSearchRequest
{
    // Every optional field is nullable, including the flags. Minimal-API binding treats a
    // non-nullable value type bound from the query string as *required* and answers an absent
    // one with an empty 400 — so a client that simply does not tick "verified only" would get a
    // failed request with no body to explain it. Null means "not asked for".

    /// <summary>Free text over name, summary and tags.</summary>
    public string? Query { get; init; }

    /// <summary>An identifier, slug, name or legacy alias. Resolved through the catalogue.</summary>
    public string? Category { get; init; }

    /// <summary>Whether to include the category's descendants. True by default.</summary>
    public bool? IncludeSubcategories { get; init; }

    /// <summary>Comma-separated <c>ProviderType</c> names.</summary>
    public string? ProviderTypes { get; init; }

    /// <summary>Comma-separated tag slugs. A provider matching any of them qualifies.</summary>
    public string? Tags { get; init; }

    /// <summary>Comma-separated BCP-47 codes.</summary>
    public string? Languages { get; init; }

    /// <summary>Comma-separated <c>ServiceDeliveryMode</c> names.</summary>
    public string? DeliveryModes { get; init; }

    /// <summary>The client's "Show only Community Verified providers" toggle.</summary>
    public bool? VerifiedOnly { get; init; }

    public bool? FeaturedOnly { get; init; }

    public bool? CertifiedOnly { get; init; }

    /// <summary>Only providers open at the moment of the request.</summary>
    public bool? OpenNowOnly { get; init; }

    public double? MinRating { get; init; }

    public int? MinReviews { get; init; }

    // ── Geography, all resolved by B4 ───────────────────────────────────────────────────────

    /// <summary>The client spells this <c>cityName</c> and, in two places, <c>city</c>.</summary>
    public string? CityName { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Kilometres, or the client's sentinels: <c>-1</c> city, <c>-2</c> country.</summary>
    public double? RadiusKm { get; init; }

    /// <summary>
    /// A map viewport: <c>north,south,east,west</c>. Wins over the point-and-radius pair.
    /// <para>
    /// Parsed and validated by B4, like every other geographic parameter on this request. A map
    /// is a rectangle, and a client that sent the smallest enclosing circle instead would fetch
    /// about a quarter more than it can show and discard the difference.
    /// </para>
    /// </summary>
    public string? Bounds { get; init; }

    /// <summary>The client's "Lister's Origin Country" filter.</summary>
    public string? OriginCountryCode { get; init; }

    // ── Ordering and paging ─────────────────────────────────────────────────────────────────

    /// <summary>The client sends <c>default</c>, <c>distance</c> or <c>rating</c>. All accepted.</summary>
    public string? Sort { get; init; }

    /// <summary>A ranking key, for a caller that wants a specific strategy by name.</summary>
    public string? Ranking { get; init; }

    /// <summary>Opaque. From a previous page's <c>nextCursor</c>.</summary>
    public string? Cursor { get; init; }

    public int? Limit { get; init; }

    /// <summary>Off by default. Counting is the expensive half of a paged query.</summary>
    public bool? IncludeTotalCount { get; init; }
}
