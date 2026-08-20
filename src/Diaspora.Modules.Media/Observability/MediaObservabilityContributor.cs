using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Media.Observability;

/// <summary>
/// Subscribes the host's OpenTelemetry pipeline to the module's traces and metrics.
/// <para>
/// The source names must match the ones <see cref="MediaTelemetry"/> creates instruments
/// behind. Declaring a name with nothing behind it subscribes the tracer to silence — which
/// looks like working observability until somebody goes looking for a span.
/// </para>
/// </summary>
public sealed class MediaObservabilityContributor : IObservabilityContributor
{
    public string Module => MediaModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [MediaTelemetry.SourceName];

    public IEnumerable<string> Meters => [MediaTelemetry.SourceName];
}
