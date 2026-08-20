namespace Diaspora.Modules.GeographicIntelligence.Contracts.V1.Responses;

/// <summary>A point on a route. Latitude and longitude, WGS 84 decimal degrees.</summary>
public sealed record RoutePointResponse
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }
}

/// <summary>One manoeuvre. The text is the provider's; this module writes none of it.</summary>
public sealed record RouteStepResponse
{
    public required string Manoeuvre { get; init; }

    public required string Instruction { get; init; }

    public required double DistanceMetres { get; init; }

    public required double DurationSeconds { get; init; }

    public RoutePointResponse? At { get; init; }
}

/// <summary>A portion of a journey travelled one way. Transit fields are null on other modes.</summary>
public sealed record RouteLegResponse
{
    public required string Mode { get; init; }

    public required double DistanceMetres { get; init; }

    public required double DurationSeconds { get; init; }

    public IReadOnlyList<RouteStepResponse> Steps { get; init; } = [];

    public string? Line { get; init; }

    public string? Agency { get; init; }

    public string? Headsign { get; init; }

    public string? DepartureStop { get; init; }

    public string? ArrivalStop { get; init; }

    public DateTimeOffset? DepartureAt { get; init; }

    public DateTimeOffset? ArrivalAt { get; init; }

    public int? StopCount { get; init; }
}

/// <summary>One planned journey.</summary>
public sealed record PlannedRouteResponse
{
    public string? Summary { get; init; }

    /// <summary>Metres. Named for its unit — §48 forbids a bare <c>distance</c>.</summary>
    public required double DistanceMetres { get; init; }

    /// <summary>Seconds, free-flow.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Seconds allowing for traffic, only where a provider modelled it. Never a copy.</summary>
    public double? DurationInTrafficSeconds { get; init; }

    public IReadOnlyList<RouteLegResponse> Legs { get; init; } = [];

    public IReadOnlyList<RoutePointResponse> Geometry { get; init; } = [];
}

/// <summary>
/// The answer to a routing request.
/// <para>
/// <b><see cref="Status"/> is the field that matters and it is never <c>Success</c> on a
/// deployment with no routing vendor.</b> The frontend has a complete routing experience running
/// against straight-line estimates, marked as estimates on every surface. What it cannot do is
/// tell the difference between "this platform has no routing" and "routing is broken today", so it
/// assumes the first forever. This response answers that question, and answers it without ever
/// putting a number where a route should be.
/// </para>
/// <para>
/// Nothing on this record is derived from a straight line. If <see cref="Routes"/> is empty the
/// client keeps its own estimate and keeps marking it as one — which is the correct outcome and
/// the reason no fallback is computed here.
/// </para>
/// </summary>
public sealed record RouteResponse
{
    /// <summary>
    /// <c>Success</c>, <c>NotConfigured</c>, <c>Unavailable</c>, <c>Unsupported</c>,
    /// <c>RateLimited</c>, <c>InvalidRequest</c> or <c>NoRoute</c>.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>Echoed so a client can confirm which mode was planned.</summary>
    public required string TravelMode { get; init; }

    /// <summary>The vendor key that answered — <c>none</c> when nothing is configured. Never a URL or a key.</summary>
    public required string Provider { get; init; }

    /// <summary>Empty unless <see cref="Status"/> is <c>Success</c>.</summary>
    public IReadOnlyList<PlannedRouteResponse> Routes { get; init; } = [];

    /// <summary>
    /// Whether the figures came from a provider that knows the roads.
    /// <para>
    /// Always false when there are no routes, and it exists so a client cannot render an empty
    /// success as a routed journey by omission. The frontend already carries an <c>isRouted</c>
    /// through every travel-time surface; this is the server half of the same guarantee.
    /// </para>
    /// </summary>
    public required bool IsRouted { get; init; }

    /// <summary>A member-readable sentence for a non-success status.</summary>
    public string? Message { get; init; }
}
