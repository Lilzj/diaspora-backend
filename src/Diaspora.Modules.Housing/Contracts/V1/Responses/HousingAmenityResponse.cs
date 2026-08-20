namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>One amenity a property has, resolved against the catalogue.</summary>
public sealed record HousingAmenityResponse
{
    public required string Key { get; init; }

    /// <summary>The catalogue name, or the key when the amenity has since been retired.</summary>
    public required string Name { get; init; }

    public string? Group { get; init; }

    public string? IconKey { get; init; }

    /// <summary>Whether this is an accessibility feature rather than a convenience.</summary>
    public required bool IsAccessibilityFeature { get; init; }

    /// <summary>The landlord qualification of the tick, where they gave one.</summary>
    public string? Note { get; init; }
}
