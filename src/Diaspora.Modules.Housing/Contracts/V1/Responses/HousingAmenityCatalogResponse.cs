namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>One entry in the amenity catalogue.</summary>
public sealed record HousingAmenityCatalogResponse
{
    public required Guid Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }

    public string? Group { get; init; }

    public string? IconKey { get; init; }

    /// <summary>Whether this is an accessibility feature rather than a convenience.</summary>
    public required bool IsAccessibilityFeature { get; init; }

    public required bool IsFilterable { get; init; }

    public required int Order { get; init; }

    public required bool IsActive { get; init; }
}
