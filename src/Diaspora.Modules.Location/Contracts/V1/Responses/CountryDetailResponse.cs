namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>A country in full: identity, metadata, geography and contributed counts.</summary>
public sealed record CountryDetailResponse
{
    public required CountrySummaryResponse Country { get; init; }

    public required CountryMetadataResponse Metadata { get; init; }

    /// <summary>A representative point, normally the capital. Null until geography is imported.</summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>The country's extent, for setting a map viewport.</summary>
    public BoundingBoxResponse? Extent { get; init; }

    public string? LandmarkImageUrl { get; init; }

    /// <summary>Contributed counts. Empty when no vertical has registered a counter.</summary>
    public required IReadOnlyDictionary<string, int> Counts { get; init; }
}
