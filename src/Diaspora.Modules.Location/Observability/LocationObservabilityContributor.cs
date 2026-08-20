using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Location.Observability;

/// <summary>Subscribes the host's tracer and metrics pipeline to the module's telemetry source.</summary>
public sealed class LocationObservabilityContributor : IObservabilityContributor
{
    public string Module => LocationModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [LocationTelemetry.SourceName];

    public IEnumerable<string> Meters => [LocationTelemetry.SourceName];
}
