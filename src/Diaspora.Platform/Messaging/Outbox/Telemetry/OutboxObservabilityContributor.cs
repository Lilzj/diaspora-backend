using Diaspora.Platform.Modules.Contributors;

namespace Diaspora.Platform.Messaging.Outbox.Telemetry;

/// <summary>
/// Subscribes the host's tracing and metrics pipelines to the outbox's instruments.
/// <para>
/// Contributed rather than wired into <c>Program.cs</c>, for the same reason every module's
/// telemetry is: the composition root should not have to learn a new source name every time
/// the platform grows one. Declaring a source name without an <c>ActivitySource</c> behind it
/// subscribes the tracer to silence, which is the mistake the module extension guide calls out
/// by name — the source and this contributor ship together.
/// </para>
/// </summary>
public sealed class OutboxObservabilityContributor : IObservabilityContributor
{
    public string Module => "Platform.Outbox";

    public IEnumerable<string> ActivitySources => [OutboxTelemetry.SourceName];

    public IEnumerable<string> Meters => [OutboxTelemetry.SourceName];
}
