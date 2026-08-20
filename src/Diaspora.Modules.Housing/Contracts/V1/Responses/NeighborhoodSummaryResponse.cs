namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>A neighbourhood as a directory card or a chip renders it.</summary>
public sealed record NeighborhoodSummaryResponse
{
    public required Guid AreaId { get; init; }

    public required string Name { get; init; }

    public required string Slug { get; init; }

    public required string CountryCode { get; init; }

    public string? CityName { get; init; }

    public string? Description { get; init; }

    public required int ListingCount { get; init; }

    public required int InsightCount { get; init; }

    public required bool IsApproved { get; init; }

    public required double Completeness { get; init; }

    /// <summary>Languages you will hear. What makes a directory card worth reading.</summary>
    public IReadOnlyList<string> PopularLanguages { get; init; } = [];
}
