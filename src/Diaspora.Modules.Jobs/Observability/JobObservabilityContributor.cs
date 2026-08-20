using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Jobs.Observability;

/// <summary>Subscribes the host's tracer and metrics to the module's telemetry source.</summary>
public sealed class JobObservabilityContributor : IObservabilityContributor
{
    public string Module => JobsModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [JobTelemetry.SourceName];

    public IEnumerable<string> Meters => [JobTelemetry.SourceName];
}
