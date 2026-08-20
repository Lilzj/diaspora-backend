using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Diaspora.Modules.Notifications.Observability;

/// <summary>
/// The module's activity source and instruments.
/// <para>
/// <b>Created here and declared in the observability contributor, always together.</b> Declaring a
/// source without creating one subscribes the tracer to silence, which is the B2 mistake every
/// module since has been careful not to repeat.
/// </para>
/// <para>
/// <b>What is counted is what an operator asks during an incident.</b> "Are notifications being
/// created" (<see cref="Created"/>), "are they going out" (<see cref="Delivered"/>), "is a channel
/// broken" (<see cref="Failed"/> tagged by channel), and three counters that catch configuration
/// mistakes nothing else would surface: a rule with no template, a template that will not render,
/// and a published fact nothing routes.
/// </para>
/// </summary>
public static class NotificationTelemetry
{
    public const string SourceName = "Diaspora.Notifications";

    public static readonly ActivitySource Activity = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> Created =
        Meter.CreateCounter<long>("diaspora.notifications.created");

    public static readonly Counter<long> Queued =
        Meter.CreateCounter<long>("diaspora.notifications.queued");

    public static readonly Counter<long> Delivered =
        Meter.CreateCounter<long>("diaspora.notifications.delivered");

    public static readonly Counter<long> Failed =
        Meter.CreateCounter<long>("diaspora.notifications.failed");

    public static readonly Counter<long> Suppressed =
        Meter.CreateCounter<long>("diaspora.notifications.suppressed");

    public static readonly Counter<long> Opened =
        Meter.CreateCounter<long>("diaspora.notifications.opened");

    public static readonly Counter<long> Clicked =
        Meter.CreateCounter<long>("diaspora.notifications.clicked");

    public static readonly Counter<long> Deduplicated =
        Meter.CreateCounter<long>("diaspora.notifications.deduplicated");

    public static readonly Counter<long> DigestsGenerated =
        Meter.CreateCounter<long>("diaspora.notifications.digests.generated");

    public static readonly Counter<long> DevicesRegistered =
        Meter.CreateCounter<long>("diaspora.notifications.devices.registered");

    public static readonly Counter<long> DevicesRetired =
        Meter.CreateCounter<long>("diaspora.notifications.devices.retired");

    /// <summary>
    /// A published fact that matched no rule.
    /// <para>
    /// Expected and common — most contracts notify nobody — but a sudden rise means a rule was
    /// deactivated or an event was renamed, and neither shows up anywhere else.
    /// </para>
    /// </summary>
    public static readonly Counter<long> TriggersWithoutRule =
        Meter.CreateCounter<long>("diaspora.notifications.triggers.unrouted");

    /// <summary>A rule naming a template nobody published. Always a mistake.</summary>
    public static readonly Counter<long> RulesWithoutTemplate =
        Meter.CreateCounter<long>("diaspora.notifications.rules.missing_template");

    /// <summary>A template that could not be rendered from the values supplied. Always a mistake.</summary>
    public static readonly Counter<long> RenderFailures =
        Meter.CreateCounter<long>("diaspora.notifications.render.failures");

    /// <summary>How far the inbox counters had drifted when reconciliation last ran.</summary>
    public static readonly Counter<long> CounterDriftRepaired =
        Meter.CreateCounter<long>("diaspora.notifications.counters.drift_repaired");

    public static readonly Histogram<double> DeliveryDuration =
        Meter.CreateHistogram<double>("diaspora.notifications.delivery.duration", "ms");

    // ── Real-time runtime ───────────────────────────────────────────────────────────────────
    //
    // Counted separately from delivery because real-time answers a different operational question.
    // A push failure means a member did not get told; a real-time drop means they will find out a
    // poll later. Mixing them would make an ordinary lock screen look like an incident, and would
    // hide the one number that does matter — connections refused, which is either an abusive client
    // or a cap set too low.

    /// <summary>A socket was accepted, tagged by client platform.</summary>
    public static readonly Counter<long> RealtimeConnected =
        Meter.CreateCounter<long>("diaspora.realtime.connections.opened");

    public static readonly Counter<long> RealtimeDisconnected =
        Meter.CreateCounter<long>("diaspora.realtime.connections.closed");

    /// <summary>
    /// A connection refused for exceeding the per-member cap.
    /// <para>
    /// The one real-time counter worth alerting on. A steady trickle is a client stuck in a
    /// reconnect loop; a step change is a cap set below what a normal member holds.
    /// </para>
    /// </summary>
    public static readonly Counter<long> RealtimeRefused =
        Meter.CreateCounter<long>("diaspora.realtime.connections.refused");

    /// <summary>A frame handed to the transport, tagged by event name.</summary>
    public static readonly Counter<long> RealtimeSent =
        Meter.CreateCounter<long>("diaspora.realtime.frames.sent");

    /// <summary>A frame the transport could not deliver. Ordinary: sockets close mid-write.</summary>
    public static readonly Counter<long> RealtimeDropped =
        Meter.CreateCounter<long>("diaspora.realtime.frames.dropped");

    /// <summary>A notification that reached a live client rather than waiting for a poll.</summary>
    public static readonly Counter<long> RealtimeDelivered =
        Meter.CreateCounter<long>("diaspora.realtime.notifications.delivered");

    /// <summary>
    /// A channel a member asked for and did not get, tagged <c>unclaimed</c> or <c>denied</c>.
    /// <para>
    /// <b>The two tags mean opposite things and both matter.</b> <c>denied</c> is the system working:
    /// somebody named a channel they may not have, and a sustained rate of it from one member is
    /// enumeration. <c>unclaimed</c> is the system misconfigured: a module that broadcasts to a
    /// prefix nobody authorises produces a channel every client is refused, silently, forever — the
    /// exact failure the prefix registry exists to make visible.
    /// </para>
    /// </summary>
    public static readonly Counter<long> RealtimeJoinsRefused =
        Meter.CreateCounter<long>("diaspora.realtime.channels.refused");
}
