using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Platform.Transactions;

/// <summary>
/// Subscribes the host's tracing and metrics pipelines to the transaction subsystem's instruments.
/// <para>
/// Contributed rather than wired into <c>Program.cs</c>, the same way the outbox and every module
/// does it — the composition root should not have to learn a new source name each time the platform
/// grows one.
/// </para>
/// </summary>
public sealed class TransactionObservabilityContributor : IObservabilityContributor
{
    public string Module => "Platform.Transactions";

    public IEnumerable<string> ActivitySources => [TransactionTelemetry.SourceName];

    public IEnumerable<string> Meters => [TransactionTelemetry.SourceName];
}
