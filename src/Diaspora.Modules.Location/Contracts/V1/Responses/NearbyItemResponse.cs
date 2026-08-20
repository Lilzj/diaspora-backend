namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// One nearby thing, as a vertical described it.
/// <para>
/// The wire form of <c>NearbyResourceItem</c>. Shaped to what the frontend's cards and
/// <c>MapPreviewCard</c> already render — a title, a category, a distance, an optional price
/// and rating — so a client can display a result from a vertical it has never heard of.
/// </para>
/// </summary>
public sealed record NearbyItemResponse
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public string? Category { get; init; }

    /// <summary>The field <c>DistanceBadge</c> and <c>NearbyIndicator</c> read.</summary>
    public required double DistanceKm { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? CityName { get; init; }

    public string? AreaName { get; init; }

    public string? ImageUrl { get; init; }

    /// <summary>Pre-formatted by the owning vertical, which owns its own currency rules.</summary>
    public string? PriceText { get; init; }

    public double? Rating { get; init; }

    /// <summary>Whatever else the owning vertical's client needs. Opaque to Location.</summary>
    public IReadOnlyDictionary<string, string>? Payload { get; init; }
}
