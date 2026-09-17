namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>Where a post is about. Field names match the client's update location.</summary>
public sealed record LocationContextResponse
{
    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    /// <summary>The client reads this as both <c>areaName</c> and <c>neighborhood</c>.</summary>
    public string? AreaName { get; init; }

    public string? CityName { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>Present only on a proximity feed. Always a distance Location computed.</summary>
    public double? DistanceKm { get; init; }

    /// <summary>
    /// True when the coordinates are where the author pinned it; false when the post sits at the
    /// centre of the place it names and a map should draw it as approximate.
    /// </summary>
    public bool PointIsExact { get; init; }
}
