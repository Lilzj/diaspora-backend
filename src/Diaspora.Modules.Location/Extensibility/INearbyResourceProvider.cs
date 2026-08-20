namespace Diaspora.Modules.Location.Extensibility;

/// <summary>
/// Implemented by a vertical to contribute its own results to Location's nearby endpoint.
/// <para>
/// <b>This is the seam that makes B4 worth building.</b> The brief asks that Community,
/// Providers, Guides, Jobs, Housing, Marketplace, Organizations, Explore, Search and
/// Recommendations require almost no geographic logic. This is how: a vertical implements one
/// method, receives a resolved point and radius, and returns its own things. It writes no
/// haversine, parses no <c>radiusKm</c>, resolves no city name, and handles no antimeridian.
/// </para>
/// <para>
/// And Location learns nothing about it in return. There is no registration list to edit, no
/// enum of resource types to extend, no reference from this assembly to any vertical.
/// Implementations are found by the same assembly scan the platform kernel uses for
/// contributors, so a new vertical appears in <c>GET /api/v1/location/nearby</c> the moment
/// its assembly ships. That is the difference between a hub and a bottleneck.
/// </para>
/// <para>
/// The frontend already needs this. Its housing detail screen makes four separate calls — to
/// jobs, to listings, to discovery, to guides — and assembles a "nearby ecosystem" panel
/// client-side, with a fallback chain for the ones that fail. One call replaces all four.
/// </para>
/// <para>
/// <b>Contract.</b> Return quickly or return nothing. Location asks each provider <b>in turn</b>
/// — they share one scoped <c>DbContext</c> and concurrent use of it is refused — so a slow
/// provider genuinely delays the ones after it, and the aggregate has a total budget that will
/// leave the tail unasked. Throwing is not a failure of the endpoint: the aggregator isolates
/// each provider, so one vertical's outage costs its own group and not the other five.
/// </para>
/// </summary>
public interface INearbyResourceProvider
{
    /// <summary>
    /// The response key this provider owns — <c>housing</c>, <c>jobs</c>, <c>marketplace</c>.
    /// Unique across the platform; a duplicate fails startup rather than letting two modules
    /// silently overwrite each other.
    /// </summary>
    string ResourceType { get; }

    /// <summary>
    /// Whether this provider should run for the request. A cheap synchronous check —
    /// a marketplace with nothing in the country need not be queried.
    /// </summary>
    bool AppliesTo(NearbyResourceRequest request) => true;

    /// <summary>This vertical's things within range, nearest first.</summary>
    Task<NearbyResourceGroup> FindNearbyAsync(
        NearbyResourceRequest request, CancellationToken cancellationToken = default);
}
