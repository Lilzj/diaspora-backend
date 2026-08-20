namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// How a message reaches a socket that is attached to a different instance.
/// <para>
/// <b>The single most consequential thing about running real-time on more than one node, declared as
/// a port so the platform cannot forget it.</b> With one instance, a hub context reaches every
/// socket there is and everything works. With two, half the members are attached to the other node
/// and half the notifications silently go nowhere — and nothing fails, nothing logs, no test catches
/// it. Naming the concept and reporting whether it is satisfied is what turns that into a startup
/// finding rather than a support ticket six weeks later.
/// </para>
/// <para>
/// <b>This interface transports nothing.</b> Every real backplane — Redis, a hosted SignalR service,
/// a message bus — is wired in at the transport layer, beneath the hub, and a port that tried to sit
/// in the message path would be reimplementing what those products exist to do. What it does is
/// <em>declare</em> what is configured, so <c>CommunicationRuntimeValidator</c> can refuse to start a
/// multi-instance deployment that has none and the health surface can report which one is in use.
/// </para>
/// </summary>
public interface IRealtimeBackplane
{
    /// <summary>
    /// What is carrying cross-instance fan-out. <c>None</c> for the shipped single-node default.
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// Whether sends reach sockets attached to other instances.
    /// <para>
    /// False means this deployment is only correct on exactly one instance. That is a legitimate
    /// configuration — it is what a single container runs — and it is a startup error the moment the
    /// deployment says it runs more than one.
    /// </para>
    /// </summary>
    bool IsDistributed { get; }

    /// <summary>
    /// Whether the backplane is reachable right now. Reported on the health surface.
    /// <para>
    /// The single-node default answers true without doing anything, because "is the current process
    /// reachable from the current process" has one answer.
    /// </para>
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
}
