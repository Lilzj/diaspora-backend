using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Observability;

/// <summary>Subscribes the host's tracer and metrics to the module's telemetry source.</summary>
public sealed class GuideObservabilityContributor : IObservabilityContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [GuideTelemetry.SourceName];

    public IEnumerable<string> Meters => [GuideTelemetry.SourceName];
}
