using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.GeographicIntelligence.Domain.Routing;

/// <summary>
/// One manoeuvre in a set of directions.
/// <para>
/// <b>The manoeuvre kind is normalised and the text is the provider's.</b> A client renders an
/// arrow from the kind and reads the sentence aloud from the text; inventing either would be the
/// fabrication the frontend register describes precisely — "a fabricated street name is
/// indistinguishable from a real one to somebody following it".
/// </para>
/// </summary>
public sealed record RouteStep
{
    /// <summary>A normalised manoeuvre: <c>turn-left</c>, <c>roundabout</c>, <c>depart</c>, …</summary>
    public required string Manoeuvre { get; init; }

    /// <summary>The provider's instruction text, verbatim. Never composed here.</summary>
    public required string Instruction { get; init; }

    /// <summary>Metres travelled on this step. Named for its unit, per §48.</summary>
    public required double DistanceMetres { get; init; }

    /// <summary>Seconds this step is expected to take. Named for its unit.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>Where the manoeuvre happens.</summary>
    public GeoCoordinate? At { get; init; }
}

/// <summary>
/// A portion of a journey travelled one way.
/// <para>
/// A walking route is one leg. A transit itinerary is several: walk, ride, transfer, ride, walk —
/// which is why <see cref="Mode"/> lives here rather than only on the route. The transit fields
/// are null on every other mode and are not invented for them.
/// </para>
/// </summary>
public sealed record RouteLeg
{
    public required TravelMode Mode { get; init; }

    public required double DistanceMetres { get; init; }

    public required double DurationSeconds { get; init; }

    public IReadOnlyList<RouteStep> Steps { get; init; } = [];

    // ── Transit, and null everywhere else ───────────────────────────────────────────────────

    /// <summary>The service's public name, as the operator publishes it. Never generated.</summary>
    public string? Line { get; init; }

    public string? Agency { get; init; }

    /// <summary>What is written on the front of the vehicle.</summary>
    public string? Headsign { get; init; }

    public string? DepartureStop { get; init; }

    public string? ArrivalStop { get; init; }

    /// <summary>
    /// Scheduled departure.
    /// <para>
    /// Absolute rather than relative, so a client can apply its own freshness rule. A departure
    /// time a few minutes old is misinformation rather than stale data, and only the client knows
    /// how long the response sat in its cache.
    /// </para>
    /// </summary>
    public DateTimeOffset? DepartureAt { get; init; }

    public DateTimeOffset? ArrivalAt { get; init; }

    public int? StopCount { get; init; }
}

/// <summary>
/// A planned journey, exactly as a provider returned it.
/// <para>
/// <b>Every figure here came from a provider that drove the roads.</b> Nothing in this record is
/// derived from a straight line, scaled by a circuity ratio, or filled in from a speed constant —
/// which is the whole reason the routing seam exists rather than a distance calculator with a
/// multiplier on it.
/// </para>
/// </summary>
public sealed record PlannedRoute
{
    /// <summary>A short human summary — the provider's, typically the road it mostly follows.</summary>
    public string? Summary { get; init; }

    public required double DistanceMetres { get; init; }

    /// <summary>Free-flow duration in seconds.</summary>
    public required double DurationSeconds { get; init; }

    /// <summary>
    /// Duration allowing for current traffic, where the provider models it.
    /// <para>
    /// <b>Null unless a provider actually supplied one</b>, and never a copy of
    /// <see cref="DurationSeconds"/>. Traffic is most of the value of a driving estimate and it
    /// cannot be derived from anything this platform holds; a client reading a present field
    /// assumes a traffic model exists behind it.
    /// </para>
    /// </summary>
    public double? DurationInTrafficSeconds { get; init; }

    public IReadOnlyList<RouteLeg> Legs { get; init; } = [];

    /// <summary>
    /// The line the journey follows.
    /// <para>
    /// Dense enough to map-match against, because the frontend's navigation machine does real
    /// geometry over whatever path it is given and a two-point line cannot be navigated.
    /// </para>
    /// </summary>
    public IReadOnlyList<GeoCoordinate> Geometry { get; init; } = [];

    /// <summary>
    /// Warnings the provider requires a member to see with this route, verbatim.
    /// <para>
    /// Some are a licence condition rather than advice: a vendor may require its "walking directions
    /// are in beta" notice on every walking route. The client shows every entry, and never
    /// paraphrases one.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
