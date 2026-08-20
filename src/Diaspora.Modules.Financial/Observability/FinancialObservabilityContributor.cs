using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Modules.Financial.Observability;

/// <summary>
/// Registers the module's activity source and meter with the host's OpenTelemetry pipeline.
/// <para>
/// Constructed by the kernel before the container exists: public parameterless constructor, no
/// dependencies.
/// </para>
/// </summary>
public sealed class FinancialObservabilityContributor : IObservabilityContributor
{
    public string Module => FinancialModuleMetadata.Name;

    public IEnumerable<string> ActivitySources => [FinancialTelemetry.SourceName];

    public IEnumerable<string> Meters => [FinancialTelemetry.SourceName];
}
