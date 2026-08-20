using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Community.Observability;

/// <summary>Subscribes the host's telemetry pipeline to the module's traces and metrics.</summary>
public sealed class CommunityObservabilityContributor : IObservabilityContributor
{
    public string Module => CommunityModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [CommunityTelemetry.SourceName];

    public IEnumerable<string> Meters => [CommunityTelemetry.SourceName];
}
