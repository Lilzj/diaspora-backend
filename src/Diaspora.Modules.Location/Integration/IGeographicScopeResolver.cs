using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Integration;

/// <summary>
/// Turns whatever geography a client sent into something a vertical can filter with.
/// <para>
/// <b>The single most reusable thing in this module.</b> The brief's success criterion is
/// that Community, Providers, Guides, Jobs, Housing, Marketplace, Explore, Search and
/// Recommendations need almost no geographic logic. Nearly all the geographic logic those
/// modules would otherwise write is this one call: take the client's loose parameters, work
/// out where the request is actually about, and hand back a bounded scope.
/// </para>
/// <para>
/// Resolution order, and why: an explicit area id wins because the client already
/// disambiguated; then coordinates, because a point is unambiguous where a name is not; then
/// a city name, resolved against the catalogue; then the member's own active location, which
/// is what "near me" means and is where travel mode is honoured. A request that supplies
/// nothing and has no member resolves to <see cref="GeographicScope.Unbounded"/> rather than
/// failing — a browse with no location is a legitimate request, and a vertical that received
/// an error for it would have to special-case the most common case.
/// </para>
/// </summary>
public interface IGeographicScopeResolver
{
    /// <summary>Resolves the request. Fails only on input that is actually invalid.</summary>
    Task<Result<GeographicScope>> ResolveAsync(
        GeographicScopeRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves the request for a member's discovery query <b>without consulting the place
    /// catalogue</b>.
    /// <para>
    /// <b>14 Oct 2026, Phase 1 of retiring the catalogue.</b> <see cref="ResolveAsync"/> snaps a
    /// point to the nearest catalogued node and resolves a typed town through the catalogue, so a
    /// town the catalogue does not hold (Mombasa, Grand Baie) was missing from every town search.
    /// This answer never carries an <see cref="GeographicScope.AreaId"/>, a
    /// <see cref="GeographicScope.Path"/> or a <see cref="GeographicScope.ViewportPath"/>:
    /// </para>
    /// <list type="bullet">
    /// <item>a viewport is its box;</item>
    /// <item>a point is a box around it plus the radius (a city-wide sentinel with no typed town
    /// becomes a fixed radius; a country-wide sentinel is the request's country);</item>
    /// <item>a typed town is the request's country plus the name, matched case-insensitively by
    /// the vertical against the town its owner typed;</item>
    /// <item>a country is the country;</item>
    /// <item>nothing, for a member, is their saved location — its point, town and country, never
    /// a node.</item>
    /// </list>
    /// <para>
    /// The request's <see cref="GeographicScopeRequest.AreaId"/> is ignored: old app builds still
    /// send one, and it must not narrow anything.
    /// </para>
    /// </summary>
    Task<Result<GeographicScope>> ResolveForDiscoveryAsync(
        GeographicScopeRequest request, CancellationToken cancellationToken = default);
}
