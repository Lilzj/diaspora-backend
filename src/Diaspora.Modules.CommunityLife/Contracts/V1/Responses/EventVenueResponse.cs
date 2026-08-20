namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>Where an event physically is.</summary>
public sealed record EventVenueResponse
{
    public string? Name { get; init; }

    public string? AddressLine1 { get; init; }

    public string? AddressLine2 { get; init; }

    /// <summary>How people will actually find it. Rendered separately, never geocoded.</summary>
    public string? Landmark { get; init; }

    public string? PostalCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? OnlineUrl { get; init; }
}
