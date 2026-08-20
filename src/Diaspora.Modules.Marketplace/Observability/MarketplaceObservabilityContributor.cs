using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Marketplace.Observability;

/// <summary>Subscribes the host's tracer and metrics to the module's telemetry source.</summary>
public sealed class MarketplaceObservabilityContributor : IObservabilityContributor
{
    public string Module => MarketplaceModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [MarketplaceTelemetry.SourceName];

    public IEnumerable<string> Meters => [MarketplaceTelemetry.SourceName];
}
