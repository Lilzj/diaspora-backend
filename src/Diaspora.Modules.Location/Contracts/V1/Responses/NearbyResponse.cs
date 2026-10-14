namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// Everything near a point, from every vertical that registered a provider.
/// <para>
/// One call replacing four. The frontend's housing detail screen currently fetches jobs,
/// listings, discovery and guides separately — with a nested try/catch fallback chain because
/// any of them may fail — and assembles the panel itself. This returns the same six groups
/// the existing <c>/location/nearby</c> mock does, keyed the same way, produced by
/// contributions rather than by Location knowing what a job is.
/// </para>
/// </summary>
public sealed record NearbyResponse
{
    /// <summary>
    /// Where the search was centred: the point, the typed town and the country.
    /// <para>
    /// Null when the request named no country and the member has none saved. Since 14 Oct 2026 a
    /// point is no longer snapped to a catalogue node, so nothing infers a country from it.
    /// </para>
    /// </summary>
    public LocationReferenceResponse? Center { get; init; }

    /// <summary>The radius applied, in kilometres. Zero for a city- or country-wide scope.</summary>
    public required double RadiusKm { get; init; }

    /// <summary><c>Radius</c>, <c>City</c> or <c>Country</c>.</summary>
    public required string Scope { get; init; }

    /// <summary>Keyed by resource type: <c>housing</c>, <c>jobs</c>, <c>marketplace</c>, …</summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<NearbyItemResponse>> Groups { get; init; }

    /// <summary>
    /// Verticals that were asked and did not answer in time or failed.
    /// <para>
    /// Reported rather than hidden. A client that cannot tell "no housing nearby" from
    /// "housing did not respond" will render the first when it means the second, and the
    /// member concludes there are no homes in their area.
    /// </para>
    /// </summary>
    public required IReadOnlyList<string> UnavailableResourceTypes { get; init; }
}
