namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// The place outlines a map viewport overlaps, and the credits they require.
/// <para>
/// <b>Retired 14 Oct 2026.</b> The platform is retiring its own place catalogue, and with it the
/// OpenStreetMap outlines it held. The route answers an empty page with <see cref="Reason"/> set
/// until Phase 3 removes it; a client must draw no outline.
/// </para>
/// </summary>
public sealed record MapBoundaryPageResponse
{
    /// <summary>The outlines, broadest place first, so a client drawing in order layers towns over regions.</summary>
    public required IReadOnlyList<MapBoundaryResponse> Boundaries { get; init; }

    /// <summary>
    /// Every distinct credit the outlines above require. A client that draws any of them shows
    /// these once beside the map.
    /// </summary>
    public required IReadOnlyList<string> Attributions { get; init; }

    /// <summary>The viewport as the server read it.</summary>
    public required BoundingBoxResponse Bounds { get; init; }

    /// <summary>
    /// False when the answer is not every outline in view: the viewport was wider than the route
    /// draws outlines for, or more outlines overlapped it than one page carries.
    /// </summary>
    public required bool IsComplete { get; init; }

    /// <summary>
    /// Added 14 Oct 2026. Why the page is empty when it is empty for a reason other than the
    /// viewport — today always <c>outlines-retired</c>.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>One place's outline, named so a client can label it and link to the place.</summary>
public sealed record MapBoundaryResponse
{
    public required Guid AreaId { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    /// <summary><c>Region</c>, <c>District</c>, <c>City</c> and so on.</summary>
    public required string Level { get; init; }

    public required string CountryCode { get; init; }

    /// <summary>The ring, with its source, licence and credit.</summary>
    public required GeographicBoundaryResponse Boundary { get; init; }
}
