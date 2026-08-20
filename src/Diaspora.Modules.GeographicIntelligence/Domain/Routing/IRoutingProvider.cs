using Diaspora.Modules.Location.Domain.ValueObjects;

namespace Diaspora.Modules.GeographicIntelligence.Domain.Routing;

/// <summary>What a caller wants routed. Validated before it reaches a provider, never after.</summary>
public sealed record RoutePlanRequest
{
    public required GeoCoordinate Origin { get; init; }

    public required GeoCoordinate Destination { get; init; }

    /// <summary>Intermediate stops, in order. Bounded by configuration before it gets here.</summary>
    public IReadOnlyList<GeoCoordinate> Waypoints { get; init; } = [];

    public required TravelMode Mode { get; init; }

    /// <summary>Whether the caller wants more than one option. Providers may answer with one.</summary>
    public bool Alternatives { get; init; }

    /// <summary>
    /// When the journey starts, for transit.
    /// <para>
    /// Meaningless on the other three modes and ignored there. Absent means "now", which is what a
    /// member tapping Directions means.
    /// </para>
    /// </summary>
    public DateTimeOffset? DepartAt { get; init; }

    /// <summary>
    /// The caller's preferred language for instruction text, as a BCP-47 tag.
    /// <para>
    /// Passed through rather than interpreted. Instruction text is prose about real streets and
    /// this module neither writes it nor translates it — §49's rule that a display name is not an
    /// identifier, applied to the one field on this seam that is prose.
    /// </para>
    /// </summary>
    public string? Language { get; init; }
}

/// <summary>
/// What a provider answered.
/// <para>
/// One type for all seven outcomes, and the constructors are the only way to make one. A caller
/// cannot build a success with no routes, and cannot build a failure that carries routes — which
/// is what stops a partially-populated response reaching a screen and being rendered as a journey.
/// </para>
/// </summary>
public sealed record RouteProviderResult
{
    private RouteProviderResult(
        RouteOutcome outcome, string providerKey, IReadOnlyList<PlannedRoute> routes, string? detail)
    {
        Outcome = outcome;
        ProviderKey = providerKey;
        Routes = routes;
        Detail = detail;
    }

    public RouteOutcome Outcome { get; }

    /// <summary>
    /// Which provider answered.
    /// <para>
    /// A stable key like <c>none</c> or <c>osrm</c> — never a URL, never a credential, and never a
    /// vendor error body. §27 forbids leaking provider configuration to a client and the simplest
    /// way to keep that is for the only provider-shaped thing that leaves this module to be a key
    /// the deployment chose.
    /// </para>
    /// </summary>
    public string ProviderKey { get; }

    public IReadOnlyList<PlannedRoute> Routes { get; }

    /// <summary>
    /// A short, safe explanation for an operator. Never the provider's raw response.
    /// </summary>
    public string? Detail { get; }

    public bool IsSuccess => Outcome == RouteOutcome.Success;

    public static RouteProviderResult Success(
        string providerKey, IReadOnlyList<PlannedRoute> routes) =>
        routes is { Count: > 0 }
            ? new RouteProviderResult(RouteOutcome.Success, providerKey, routes, detail: null)
            : new RouteProviderResult(RouteOutcome.NoRoute, providerKey, [], detail: null);

    public static RouteProviderResult NotConfigured(string providerKey = "none") =>
        new(RouteOutcome.NotConfigured, providerKey, [], detail: null);

    public static RouteProviderResult Unavailable(string providerKey, string? detail = null) =>
        new(RouteOutcome.Unavailable, providerKey, [], detail);

    public static RouteProviderResult Unsupported(string providerKey, string? detail = null) =>
        new(RouteOutcome.Unsupported, providerKey, [], detail);

    public static RouteProviderResult RateLimited(string providerKey, string? detail = null) =>
        new(RouteOutcome.RateLimited, providerKey, [], detail);

    public static RouteProviderResult InvalidRequest(string providerKey, string? detail = null) =>
        new(RouteOutcome.InvalidRequest, providerKey, [], detail);

    public static RouteProviderResult NoRoute(string providerKey) =>
        new(RouteOutcome.NoRoute, providerKey, [], detail: null);
}

/// <summary>
/// The boundary an external routing vendor plugs into.
/// <para>
/// <b>Provider-neutral by construction.</b> Nothing in this interface or the records around it
/// names Google, Mapbox, OSRM, HERE or TomTom, mentions an API key, or accepts a URL. A vendor
/// adapter lives in this module's <c>Infrastructure</c> namespace, reads its own configuration
/// section, and is the only place a credential appears. A client cannot influence which provider
/// is called or where it is called — which is what closes the SSRF shape the phase brief's §27
/// asks about, because there is no client-supplied endpoint to close.
/// </para>
/// <para>
/// <b>Why this seam ships with no vendor behind it, when PSR-1.2's rule is that a stub is worse
/// than an absence.</b> That rule was written after three waves each found a seam that was
/// complete, registered, architecture-tested and reachable by nobody. This one is reachable: it
/// has a route in front of it, that route is authorised, instrumented and tested, and it answers
/// <see cref="RouteOutcome.NotConfigured"/> — which is a fact the frontend has been asking for
/// since Phase 8 and currently has to assume. The rule forbids a seam with no consumer, not a
/// seam whose consumer is told the truth.
/// </para>
/// </summary>
public interface IRoutingProvider
{
    /// <summary>A stable key identifying the vendor. Safe to publish; never a credential.</summary>
    string Key { get; }

    /// <summary>
    /// Which modes this provider answers.
    /// <para>
    /// Declared rather than inferred. A provider that routes cars, bicycles and pedestrians and
    /// has no timetable answers three of the four, and the caller must be able to know that
    /// without asking and getting an error.
    /// </para>
    /// </summary>
    IReadOnlyCollection<TravelMode> SupportedModes { get; }

    /// <summary>
    /// Plans a journey.
    /// <para>
    /// <b>Returns an outcome and never throws for a provider condition.</b> A vendor being down is
    /// an ordinary Tuesday, and an implementation that threw would make every caller wrap it —
    /// which is how a provider outage becomes a 500 on a discovery screen that had six other
    /// things to show.
    /// </para>
    /// </summary>
    Task<RouteProviderResult> PlanAsync(
        RoutePlanRequest request, CancellationToken cancellationToken = default);
}
