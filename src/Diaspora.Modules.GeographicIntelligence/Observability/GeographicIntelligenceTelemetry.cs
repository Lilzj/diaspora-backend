using System.Diagnostics;
using System.Diagnostics.Metrics;
using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.GeographicIntelligence.Observability;

/// <summary>
/// The module's activity source and meters.
/// <para>
/// <b>Nothing here carries a coordinate.</b> The phase brief's §41 asks for provider latency and
/// search rates and, in the same sentence, for exact locations not to be logged. Both hold only if
/// the tag set is decided once, here: every tag on every counter below is a low-cardinality label
/// — a surface, a layer, a travel mode, a refusal reason, a provider key — and none of them is a
/// latitude, a longitude, a bounding box or a member. A counter tagged with a viewport would be a
/// movement log with a metrics bill attached.
/// </para>
/// <para>
/// The pairing that matters most is <see cref="DimensionsServed"/> against
/// <see cref="DimensionsUnavailable"/>. A deployment whose unavailable count is most of its
/// traffic has neighbourhood profiles nobody has filled in, and that is invisible from any
/// infrastructure dashboard — the endpoint is healthy, fast and answering almost nothing.
/// </para>
/// </summary>
public static class GeographicIntelligenceTelemetry
{
    /// <summary>The name the observability contributor subscribes the host's tracer to.</summary>
    public const string SourceName = "Diaspora.GeographicIntelligence";

    public static ActivitySource Source { get; } = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    // ── Reads ───────────────────────────────────────────────────────────────────────────────

    /// <summary>Intelligence reads, tagged by surface (scores, cost-of-living, safety, heat).</summary>
    public static Counter<long> Requests { get; } =
        Meter.CreateCounter<long>(
            "diaspora.geo.requests", "request", "Geographic intelligence reads.");

    /// <summary>How long a read took, tagged by surface. The composition cost, not the provider's.</summary>
    public static Histogram<double> Duration { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.geo.duration", "ms", "Geographic intelligence read duration.");

    /// <summary>Reads that failed, tagged by error code.</summary>
    public static Counter<long> Errors { get; } =
        Meter.CreateCounter<long>(
            "diaspora.geo.errors", "error", "Geographic intelligence reads that failed.");

    // ── Composition ─────────────────────────────────────────────────────────────────────────

    /// <summary>Score dimensions served with a real source behind them, tagged by dimension.</summary>
    public static Counter<long> DimensionsServed { get; } =
        Meter.CreateCounter<long>(
            "diaspora.geo.dimensions.served", "dimension",
            "Score dimensions served from an authoritative source.");

    /// <summary>
    /// Score dimensions withheld, tagged by dimension and reason.
    /// <para>
    /// The figure that says whether this platform's geographic intelligence is thin because nobody
    /// has assessed the places, or because a data source was never contracted. Those need different
    /// responses and look identical from outside.
    /// </para>
    /// </summary>
    public static Counter<long> DimensionsUnavailable { get; } =
        Meter.CreateCounter<long>(
            "diaspora.geo.dimensions.unavailable", "dimension",
            "Score dimensions withheld for want of a source.");

    /// <summary>Heat cells dropped for falling under the suppression floor.</summary>
    public static Counter<long> CellsSuppressed { get; } =
        Meter.CreateCounter<long>(
            "diaspora.geo.cells.suppressed", "cell",
            "Density cells withheld by the minimum-count rule.");

    // ── Input validation ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Coordinates refused, tagged by which rule refused them.
    /// <para>
    /// A climbing rate here is usually a client sending longitude where latitude belongs, which is
    /// silent everywhere else: both are numbers and both are plausible.
    /// </para>
    /// </summary>
    public static Counter<long> InvalidCoordinates { get; } =
        Meter.CreateCounter<long>(
            "diaspora.geo.coordinates.invalid", "coordinate",
            "Coordinates refused by range or shape validation.");

    /// <summary>Requests refused for asking beyond the configured radius or waypoint ceiling.</summary>
    public static Counter<long> BoundsRejected { get; } =
        Meter.CreateCounter<long>(
            "diaspora.geo.bounds.rejected", "request",
            "Requests refused for exceeding a configured geographic bound.");

    // ── Providers ───────────────────────────────────────────────────────────────────────────

    /// <summary>Calls offered to a geographic provider, tagged by capability and provider key.</summary>
    public static Counter<long> ProviderRequests { get; } =
        Meter.CreateCounter<long>(
            "diaspora.geo.provider.requests", "request",
            "Calls offered to an external geographic provider.");

    /// <summary>
    /// Provider calls that did not succeed, tagged by provider key and outcome.
    /// <para>
    /// <c>NotConfigured</c> is an outcome here rather than an absence, so a deployment that has
    /// never contracted a routing vendor sees the demand it is refusing.
    /// </para>
    /// </summary>
    public static Counter<long> ProviderFailures { get; } =
        Meter.CreateCounter<long>(
            "diaspora.geo.provider.failures", "request",
            "Provider calls that returned other than success.");

    /// <summary>Provider round-trip duration, tagged by capability and provider key.</summary>
    public static Histogram<double> ProviderLatency { get; } =
        Meter.CreateHistogram<double>(
            "diaspora.geo.provider.latency", "ms", "External geographic provider latency.");
}

/// <summary>
/// Subscribes the host's tracer and metrics to the module's telemetry source.
/// <para>
/// The <c>ActivitySource</c> and <c>Meter</c> behind this name are created in
/// <see cref="GeographicIntelligenceTelemetry"/>. Declaring a source without creating one
/// subscribes the tracer to silence, which is the B2 mistake the contributor contract calls out.
/// </para>
/// </summary>
public sealed class GeographicIntelligenceObservabilityContributor : IObservabilityContributor
{
    public string Module => GeographicIntelligenceModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [GeographicIntelligenceTelemetry.SourceName];

    public IEnumerable<string> Meters => [GeographicIntelligenceTelemetry.SourceName];
}
