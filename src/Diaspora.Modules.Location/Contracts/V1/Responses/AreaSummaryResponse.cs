namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>A hierarchy node as it appears in a list or a search result.</summary>
public sealed record AreaSummaryResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    /// <summary>The endonym, when it differs from the English name.</summary>
    public string? LocalName { get; init; }

    public required string Slug { get; init; }

    /// <summary><c>StateProvince</c>, <c>Region</c>, <c>District</c>, <c>City</c>,
    /// <c>Neighborhood</c>, <c>PostalArea</c>.</summary>
    public required string Level { get; init; }

    public required string CountryCode { get; init; }

    public required string Flag { get; init; }

    public Guid? ParentId { get; init; }

    /// <summary>
    /// The full ancestor path, e.g. <c>/GB/greater-london/london/peckham</c>. A client can
    /// render a breadcrumb from this without a second request.
    /// </summary>
    public required string Path { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Present only on proximity results.</summary>
    public double? DistanceKm { get; init; }

    /// <summary>
    /// The one-line rendering: "Peckham, London". Pre-composed so a list does not have to
    /// join the breadcrumb itself.
    /// </summary>
    public required string DisplayName { get; init; }
}
