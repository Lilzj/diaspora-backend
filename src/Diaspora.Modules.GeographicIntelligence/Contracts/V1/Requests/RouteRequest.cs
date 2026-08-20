namespace Diaspora.Modules.GeographicIntelligence.Contracts.V1.Requests;

/// <summary>A coordinate on a routing request. Decimal degrees, WGS 84.</summary>
public sealed record RoutePointRequest
{
    public double Latitude { get; init; }

    public double Longitude { get; init; }
}

/// <summary>
/// A journey to plan.
/// <para>
/// <b>POST rather than GET, and the reason is privacy rather than length.</b> An origin is the
/// most sensitive coordinate this platform accepts — for a member routing from where they stand it
/// is where they stand — and a query string is logged by every proxy, gateway and access log
/// between the device and the handler. A body is not. Phase 8's own register makes the same
/// argument for proxying routing through the backend at all.
/// </para>
/// <para>
/// There is deliberately no provider field and no endpoint field. A client cannot choose or
/// influence which vendor is called, which is what makes the SSRF question the phase brief's §27
/// raises unanswerable in the right direction.
/// </para>
/// </summary>
public sealed record RouteRequest
{
    public RoutePointRequest? Origin { get; init; }

    public RoutePointRequest? Destination { get; init; }

    /// <summary>Intermediate stops in order. Bounded by configuration.</summary>
    public IReadOnlyList<RoutePointRequest>? Waypoints { get; init; }

    /// <summary><c>walking</c>, <c>cycling</c>, <c>driving</c> or <c>transit</c>. Required.</summary>
    public string? TravelMode { get; init; }

    /// <summary>Whether to ask for more than one option. A provider may still return one.</summary>
    public bool Alternatives { get; init; }

    /// <summary>Departure time, for transit. Absent means now.</summary>
    public DateTimeOffset? DepartAt { get; init; }

    /// <summary>Preferred language for instruction text, BCP-47. Passed through, never interpreted.</summary>
    public string? Language { get; init; }
}
