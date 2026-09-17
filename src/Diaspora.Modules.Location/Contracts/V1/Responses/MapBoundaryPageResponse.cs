namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// The place outlines a map viewport overlaps, and the credits they require.
/// <para>
/// Only places whose outline the catalogue holds appear — today the towns, districts and regions
/// with an exact OpenStreetMap match. A place that is absent has no stored outline; it is not
/// outside the viewport, and a client must not draw one for it.
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
