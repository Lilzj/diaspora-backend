using Diaspora.Modules.Location.Domain.ValueObjects;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Location.Domain.Spatial;

/// <summary>
/// Turns coordinates into a place, and a place into coordinates.
/// <para>
/// Separate from <see cref="IGeospatialQueryProvider"/> because the two have different
/// failure modes and different costs. A spatial query runs against data the platform owns and
/// either works or does not; geocoding usually calls someone else's network service, is
/// rate-limited, is billed per request, and can be down. Keeping them apart means a Mapbox
/// outage degrades address lookup without taking radius search with it.
/// </para>
/// <para>
/// The frontend currently calls <c>ipapi.co</c> directly from the device during
/// registration, which sends every prospective member's IP address to a third party the
/// platform has no agreement with and cannot rate-limit. This interface is where the
/// server-side replacement plugs in.
/// </para>
/// </summary>
public interface IGeocodingProvider
{
    /// <summary>The configuration key that selects this provider.</summary>
    string Key { get; }

    /// <summary>
    /// Whether this provider reaches outside the process. Callers use it to decide whether a
    /// result may be cached aggressively and whether a failure is worth retrying.
    /// </summary>
    bool IsExternal { get; }

    /// <summary>The place a coordinate falls in.</summary>
    Task<Result<LocationSnapshot>> ReverseGeocodeAsync(
        GeoCoordinate coordinate, CancellationToken cancellationToken = default);

    /// <summary>
    /// The coordinates of a described place. Returns candidates, best first — an address is
    /// rarely unambiguous and pretending otherwise pushes the ambiguity somewhere worse.
    /// </summary>
    Task<Result<IReadOnlyList<LocationSnapshot>>> ForwardGeocodeAsync(
        string query, CountryCode? within, CancellationToken cancellationToken = default);
}
