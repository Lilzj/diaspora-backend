using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Providers.Observability;

/// <summary>Subscribes the host's telemetry pipeline to the module's traces and metrics.</summary>
public sealed class ProviderObservabilityContributor : IObservabilityContributor
{
    public string Module => ProvidersModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [ProviderTelemetry.SourceName];

    public IEnumerable<string> Meters => [ProviderTelemetry.SourceName];
}
