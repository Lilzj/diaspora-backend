using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Location.Observability;

/// <summary>
/// The module's live telemetry instruments.
/// <para>
/// Declared alongside the contributor that subscribes the host's pipeline to them, and both
/// are required. Declaring a source name without creating an <see cref="ActivitySource"/>
/// behind it subscribes the tracer to silence — the B2 mistake the module extension guide
/// calls out by name.
/// </para>
/// <para>
/// The instruments were chosen for the questions this module will actually be asked in
/// production: is hyperlocal search slow, is it slow because a vertical is slow, and is the
/// geocoder finding anything.
/// </para>
/// </summary>
public static class LocationTelemetry
{
    public const string SourceName = "Diaspora.Location";

    public const string Version = "1.0.0";

    public static readonly ActivitySource Activity = new(SourceName, Version);

    public static readonly Meter Meter = new(SourceName, Version);

    /// <summary>Hyperlocal queries served, tagged by kind.</summary>
    public static readonly Counter<long> SpatialQueries =
        Meter.CreateCounter<long>(
            "diaspora.location.spatial_queries",
            unit: "{query}",
            description: "Radius, bounding-box, polygon and nearest queries served.");

    /// <summary>End-to-end time for a spatial query, provider included.</summary>
    public static readonly Histogram<double> SpatialQueryDuration =
        Meter.CreateHistogram<double>(
            "diaspora.location.spatial_query_duration",
            unit: "ms",
            description: "Time to answer a hyperlocal query.");

    /// <summary>Nearby providers invoked. Divided by request count, the fan-out width.</summary>
    public static readonly Counter<long> NearbyProvidersQueried =
        Meter.CreateCounter<long>(
            "diaspora.location.nearby_providers_queried",
            unit: "{provider}",
            description: "Vertical nearby-resource providers invoked.");

    /// <summary>
    /// Providers that timed out or threw. The alert that matters: a rising count here means a
    /// vertical is degrading while Location's own latency stays flat, which is invisible in
    /// endpoint metrics alone.
    /// </summary>
    public static readonly Counter<long> NearbyProviderFailures =
        Meter.CreateCounter<long>(
            "diaspora.location.nearby_provider_failures",
            unit: "{provider}",
            description: "Nearby providers that failed or exceeded their budget.");

    /// <summary>Per-provider latency, tagged by resource type.</summary>
    public static readonly Histogram<double> NearbyProviderDuration =
        Meter.CreateHistogram<double>(
            "diaspora.location.nearby_provider_duration",
            unit: "ms",
            description: "Time each vertical took to answer a nearby query.");

    /// <summary>Place-name searches served.</summary>
    public static readonly Counter<long> LocationSearches =
        Meter.CreateCounter<long>(
            "diaspora.location.searches",
            unit: "{search}",
            description: "Place-name autocomplete queries served.");

    /// <summary>
    /// Searches that matched nothing. A rising ratio against
    /// <see cref="LocationSearches"/> is the signal that the catalogue needs a wider import —
    /// the failure members experience as "my city is not here".
    /// </summary>
    public static readonly Counter<long> EmptySearches =
        Meter.CreateCounter<long>(
            "diaspora.location.empty_searches",
            unit: "{search}",
            description: "Place-name queries that returned no results.");

    /// <summary>Reverse-geocode calls, tagged by whether they resolved.</summary>
    public static readonly Counter<long> ReverseGeocodes =
        Meter.CreateCounter<long>(
            "diaspora.location.reverse_geocodes",
            unit: "{lookup}",
            description: "Coordinate-to-place lookups served.");

    /// <summary>Residency projections written from Trust's events.</summary>
    public static readonly Counter<long> ResidencyProjections =
        Meter.CreateCounter<long>(
            "diaspora.location.residency_projections",
            unit: "{event}",
            description: "Trust verification events applied to the residency projection.");
}
