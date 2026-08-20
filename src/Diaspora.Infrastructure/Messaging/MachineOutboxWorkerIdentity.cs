using System.Diagnostics;
using System.Globalization;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;

namespace Diaspora.Infrastructure.Messaging;

/// <summary>
/// Identifies this process as host, process id and a per-start suffix.
/// <para>
/// The suffix matters more than it looks. In a container the host name is the pod name and the
/// process id is almost always 1, so two successive starts of the same pod would share an
/// identity — and a restarted worker could release a lease its own previous incarnation still
/// held, if that process were somehow still running. A random suffix per start makes every
/// worker instance distinct without needing coordination.
/// </para>
/// <para>
/// Read from the environment rather than configured, because a configured worker identity is
/// one that gets copied into a second deployment's settings file and quietly duplicated.
/// </para>
/// </summary>
public sealed class MachineOutboxWorkerIdentity : IOutboxWorkerIdentity
{
    public MachineOutboxWorkerIdentity()
    {
        var host = Environment.MachineName;
        var processId = Environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        var incarnation = Guid.NewGuid().ToString("N")[..8];

        var identity = $"{host}/{processId}/{incarnation}";

        WorkerId = identity.Length <= OutboxFieldLimits.WorkerId
            ? identity
            : identity[^OutboxFieldLimits.WorkerId..];

        Activity.Current?.SetTag("outbox.worker", WorkerId);
    }

    public string WorkerId { get; }
}
