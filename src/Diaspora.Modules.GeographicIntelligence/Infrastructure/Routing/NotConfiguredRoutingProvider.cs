using Diaspora.Modules.GeographicIntelligence.Domain.Routing;

namespace Diaspora.Modules.GeographicIntelligence.Infrastructure.Routing;

/// <summary>
/// The routing provider a deployment has until it contracts one.
/// <para>
/// <b>It answers <see cref="RouteOutcome.NotConfigured"/> and it is not a stub.</b> The
/// distinction is worth being exact about, because PSR-1.2 recorded the opposite rule after three
/// waves each found a seam that was complete, registered, architecture-tested and reachable by
/// nobody — "a stub is worse than an absence". A stub is a seam that pretends the capability
/// exists; this is a seam that states, in a form a client can branch on, that it does not.
/// </para>
/// <para>
/// <b>What it must never do is the thing it would be easiest to do.</b> Every input needed to
/// fabricate a plausible answer is right here: two coordinates, a travel mode, and B4's distance
/// calculator one injection away. Multiply the straight line by a published circuity ratio, divide
/// by a walking speed, and this class returns a route. It would be wrong on any river, it would be
/// indistinguishable from a real answer at the wire, and the frontend — which currently computes
/// exactly that figure and labels it an estimate on every surface — would start presenting it as a
/// routed time, because a backend said so. §54 forbids it and this class is where the temptation
/// actually lives.
/// </para>
/// <para>
/// <see cref="SupportedModes"/> is empty rather than listing four modes it cannot serve, so a
/// client reading the capability report gets the same answer as one calling the route.
/// </para>
/// </summary>
public sealed class NotConfiguredRoutingProvider : IRoutingProvider
{
    /// <summary>The key published on every response. Names no vendor because there is none.</summary>
    public string Key => "none";

    public IReadOnlyCollection<TravelMode> SupportedModes => [];

    public Task<RouteProviderResult> PlanAsync(
        RoutePlanRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult(RouteProviderResult.NotConfigured(Key));
}
