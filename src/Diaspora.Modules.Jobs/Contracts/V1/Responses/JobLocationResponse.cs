namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>Where a job is.</summary>
public sealed record JobLocationResponse
{
    /// <summary>Upper-case ISO 3166-1 alpha-2. Always present, even for a remote role.</summary>
    public required string CountryCode { get; init; }

    public string? RegionName { get; init; }

    public string? CityName { get; init; }

    public Guid? AreaId { get; init; }

    /// <summary>The most specific place named. What a card prints.</summary>
    public required string DisplayName { get; init; }
}
