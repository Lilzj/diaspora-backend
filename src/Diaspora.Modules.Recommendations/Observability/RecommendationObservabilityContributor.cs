using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Recommendations.Observability;

/// <summary>
/// Subscribes the host's tracer and metrics to the module's telemetry source.
/// <para>
/// The <c>ActivitySource</c> and <c>Meter</c> behind these names are created in
/// <see cref="RecommendationTelemetry"/>. Declaring a source without creating one subscribes the
/// tracer to silence, so both are always done together.
/// </para>
/// </summary>
public sealed class RecommendationObservabilityContributor : IObservabilityContributor
{
    public string Module => RecommendationsModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [RecommendationTelemetry.SourceName];

    public IEnumerable<string> Meters => [RecommendationTelemetry.SourceName];
}
