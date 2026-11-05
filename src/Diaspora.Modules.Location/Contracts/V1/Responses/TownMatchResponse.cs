namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// The town a typed place name means, from the world list (GeoNames, CC BY 4.0), or
/// <see cref="Found"/> false when none is close enough. The app then asks its other town search.
/// </summary>
public sealed record TownMatchResponse
{
    public required bool Found { get; init; }

    public string? Name { get; init; }

    public string? CountryCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public long? Population { get; init; }

    /// <summary>True when the name was typed as the list has it; false for a close spelling.</summary>
    public bool Exact { get; init; }

    public static TownMatchResponse NotFound { get; } = new() { Found = false };
}
