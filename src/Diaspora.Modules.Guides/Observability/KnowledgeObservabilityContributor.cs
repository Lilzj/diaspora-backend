using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Guides.Observability;

/// <summary>
/// Subscribes the host's tracer and metrics to the Knowledge Directory's telemetry source.
/// <para>
/// A second contributor from the same module, which the kernel accepts because contributors are
/// collected as a set rather than keyed by module. Declaring both sources from one contributor
/// would work equally well and read worse: the two sources have different volumes and different
/// audiences, and a deployment turning one off should be able to see that it can.
/// </para>
/// </summary>
public sealed class KnowledgeObservabilityContributor : IObservabilityContributor
{
    public string Module => GuidesModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [KnowledgeTelemetry.SourceName];

    public IEnumerable<string> Meters => [KnowledgeTelemetry.SourceName];
}
