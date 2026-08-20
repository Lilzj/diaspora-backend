using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Application.Observability;

/// <summary>
/// Subscribes the host's OpenTelemetry pipeline to the application layer's instruments.
/// </summary>
public sealed class ApplicationObservabilityContributor : IObservabilityContributor
{
    public string Module => "Application";

    public IEnumerable<string> ActivitySources => [ApplicationTelemetry.SourceName];

    public IEnumerable<string> Meters => [ApplicationTelemetry.SourceName];
}
