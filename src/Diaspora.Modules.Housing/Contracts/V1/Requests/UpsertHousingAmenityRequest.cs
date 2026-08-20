namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>Creates or revises an amenity.</summary>
public sealed record UpsertHousingAmenityRequest
{
    /// <summary>Required on create, ignored on update. Listings store it.</summary>
    public string? Key { get; init; }

    public required string Name { get; init; }

    /// <summary>The presentation group: utilities, safety, connectivity, outdoor space.</summary>
    public string? Group { get; init; }

    public string? IconKey { get; init; }

    /// <summary>
    /// Whether this is an accessibility feature rather than a convenience. Called out separately
    /// because for the members who need it, it decides whether a property is habitable at all.
    /// </summary>
    public bool IsAccessibilityFeature { get; init; }

    /// <summary>Whether the search facets offer it.</summary>
    public bool IsFilterable { get; init; } = true;

    public int Order { get; init; }

    public bool? IsActive { get; init; }
}
