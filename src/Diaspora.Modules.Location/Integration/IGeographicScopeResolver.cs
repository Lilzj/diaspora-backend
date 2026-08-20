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
}
