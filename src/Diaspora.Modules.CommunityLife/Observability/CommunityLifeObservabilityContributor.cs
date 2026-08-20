using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.CommunityLife.Observability;

/// <summary>
/// Subscribes the host's tracer and metrics to the module's telemetry source.
/// <para>
/// The <c>ActivitySource</c> and <c>Meter</c> behind these names are created in
/// <see cref="CommunityLifeTelemetry"/>. Declaring a source without creating one is the B2 mistake —
/// it subscribes the tracer to silence — so both are always done together.
/// </para>
/// </summary>
public sealed class CommunityLifeObservabilityContributor : IObservabilityContributor
{
    public string Module => CommunityLifeModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [CommunityLifeTelemetry.SourceName];

    public IEnumerable<string> Meters => [CommunityLifeTelemetry.SourceName];
}
