namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// One nearby thing, described in terms every vertical can supply and no vertical is
/// constrained by.
/// <para>
/// The shape is drawn from what the frontend's map and cards actually render — a title, a
/// category, a distance, an optional price and rating — plus <see cref="Payload"/> for
/// whatever else a vertical wants to hand its own client. The alternative, letting each
/// provider return its own DTO, would make the aggregate response a union type the client has
/// to switch on, and would put every vertical's schema into Location's contract.
/// </para>
/// </summary>
public sealed record NearbyResourceItem
{
    /// <summary>The vertical's own identifier for the thing.</summary>
    public required string Id { get; init; }

    public required string Title { get; init; }

    /// <summary>What kind of thing it is, in the vertical's own vocabulary.</summary>
    public string? Category { get; init; }

    /// <summary>How far from the search centre. The field the frontend renders as
    /// <c>distanceKm</c>.</summary>
    public required double DistanceKm { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public string? CityName { get; init; }

    /// <summary>The frontend also calls this <c>neighborhood</c>. Same value.</summary>
    public string? AreaName { get; init; }

    public string? ImageUrl { get; init; }

    /// <summary>Pre-formatted by the vertical, which owns its own currency rules.</summary>
    public string? PriceText { get; init; }

    public double? Rating { get; init; }

    /// <summary>
    /// Anything else the vertical's own client needs. Serialised verbatim; Location neither
    /// reads it nor validates it.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Payload { get; init; }
}
