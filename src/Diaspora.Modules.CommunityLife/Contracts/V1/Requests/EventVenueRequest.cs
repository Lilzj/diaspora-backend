namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Where an event physically is.</summary>
public sealed record EventVenueRequest
{
    /// <summary>What the place is called — the hall, the church, the community centre.</summary>
    public string? Name { get; init; }

    public string? AddressLine1 { get; init; }

    public string? AddressLine2 { get; init; }

    /// <summary>
    /// How people will actually find it — "opposite the Total filling station". Its own field, never
    /// sent to a geocoder, and the most locally-informed decision in the module.
    /// </summary>
    public string? Landmark { get; init; }

    public string? PostalCode { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Where an online or hybrid event happens. Must be an http(s) URL.</summary>
    public string? OnlineUrl { get; init; }
}
