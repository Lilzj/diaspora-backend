using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Observability;

/// <summary>
/// Subscribes the host's tracer and metrics to the Country Intelligence Platform's telemetry source.
/// <para>
/// A third contributor from the same module, which the kernel accepts because contributors are
/// collected as a set rather than keyed by module. Three sources rather than one because they have
/// different volumes and different audiences — and a deployment turning one off should be able to see
/// that it can.
/// </para>
/// </summary>
public sealed class IntelligenceObservabilityContributor : IObservabilityContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [IntelligenceTelemetry.SourceName];

    public IEnumerable<string> Meters => [IntelligenceTelemetry.SourceName];
}
