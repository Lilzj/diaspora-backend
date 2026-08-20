using Diaspora.Platform.Modules.Contributors;
using Diaspora.Platform.Operations.Telemetry;

namespace Diaspora.Operations.Telemetry;

/// <summary>
/// Subscribes the host's OpenTelemetry pipeline to the Production Platform's instruments.
/// <para>
/// The same contributor shape every module uses, and it carries the same obligation B2.6 wrote into
/// <see cref="IObservabilityContributor"/>: the names here must match live instruments. They do —
/// <see cref="OperationsTelemetry"/> owns both — and the architecture suite asserts it, because a
/// contributor naming a source nothing creates produces a tracer subscribed to silence, which is
/// exactly the defect B2 shipped and B2.6 found.
/// </para>
/// </summary>
public sealed class OperationsObservabilityContributor : IObservabilityContributor
{
    public string Module => "Platform";

    public IEnumerable<string> ActivitySources => [PlatformTelemetryNames.OperationsActivitySource];

    public IEnumerable<string> Meters => [PlatformTelemetryNames.OperationsMeter];
}
