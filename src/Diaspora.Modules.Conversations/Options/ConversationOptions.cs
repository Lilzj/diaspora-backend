using System.ComponentModel.DataAnnotations;

namespace Diaspora.Modules.Conversations.Options;

/// <summary>
/// What a deployment can tune.
/// <para>
/// <b>The line against <c>ConversationLimits</c> is whether changing it invalidates rows already
/// written.</b> A shorter message body would; a lower send rate would not. Anything a stored
/// conversation is valid against is a domain constant and is compiled in.
/// </para>
/// </summary>
public sealed class ConversationOptions
{
    public const string SectionName = "Conversations";

    /// <summary>
    /// Whether the platform serves conversations at all.
    /// <para>
    /// False leaves the endpoints mapped and refusing, rather than unmapped. A route that vanishes
    /// gives a client a 404 it cannot distinguish from a bad build; a route that answers "switched
    /// off" is a state the client can render.
    /// </para>
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether messages may carry Media references.</summary>
    public bool EnableAttachments { get; set; } = true;

    /// <summary>
    /// Whether a live message write is attempted at all.
    /// <para>
    /// Independent of B13's own realtime switch: a deployment may want notifications live and
    /// conversation traffic polled while it sizes a backplane. With this off, messages still land in
    /// the database and still push.
    /// </para>
    /// </summary>
    public bool EnableRealtime { get; set; } = true;

    /// <summary>Whether a new message produces a notification.</summary>
    public bool EnablePushNotifications { get; set; } = true;

    /// <summary>
    /// How many messages one member may send per minute, across every conversation.
    /// <para>
    /// Generous, because the shape being refused is a script rather than a fast typist. Enforced
    /// through the platform's subject rate limiter so the counter is shared with every other
    /// per-member limit rather than being a second, disagreeing one.
    /// </para>
    /// </summary>
    [Range(1, 600)]
    public int MessagesPerMinute { get; set; } = 60;

    /// <summary>
    /// How many conversations one member may open per hour.
    /// <para>
    /// The rate that matters for abuse: sending into a conversation somebody accepted is ordinary,
    /// opening two hundred conversations with strangers in an hour is not.
    /// </para>
    /// </summary>
    [Range(1, 500)]
    public int ConversationsPerHour { get; set; } = 30;

    /// <summary>
    /// Whether a conversation with somebody a member has no shared context with starts as a request.
    /// <para>
    /// The catalogue decides per type; this is the floor. Off, and a deployment has no message
    /// requests at all — which is a legitimate choice for a closed community and a bad one for an
    /// open platform.
    /// </para>
    /// </summary>
    public bool EnableMessageRequests { get; set; } = true;

    /// <summary>How long a closed conversation is kept before retention may purge it.</summary>
    [Range(30, 3650)]
    public int RetentionDays { get; set; } = 730;

    // ── The attention sweep — B23.x Wave 12 ─────────────────────────────────────────────────────
    //
    // Two facts this module publishes about conversations that have *not* changed, because the
    // Phase 18 automation catalogue offers `conversation.inactive` and `conversation.unreadReminder`
    // and deriving either outside this module would mean another module reading message state.

    /// <summary>
    /// Whether the sweep runs at all.
    /// <para>
    /// Off leaves the contracts declared and unpublished, which is the honest state for a deployment
    /// that does not want the platform noticing silence — and it is a switch rather than a code path,
    /// so turning it on needs no deploy.
    /// </para>
    /// </summary>
    public bool EnableAttentionSweep { get; set; } = true;

    /// <summary>
    /// How long a conversation must be silent before the platform says so.
    /// <para>
    /// <b>Three days, and the number is deliberately not shorter.</b> A day of quiet in a thread
    /// about a flat is somebody thinking it over; a working week of it is a conversation that has
    /// stalled. The failure mode of a short window is a reminder that reads as nagging, which is how
    /// a member turns the trigger off and never turns it back on.
    /// </para>
    /// </summary>
    [Range(1, 90)]
    public int InactivityAfterDays { get; set; } = 3;

    /// <summary>
    /// How long unread must have been sitting before it counts as a backlog.
    /// <para>
    /// Shorter than the inactivity window because it is a different question: unread for a day is
    /// somebody who has not opened the app, and telling them is the point of the trigger.
    /// </para>
    /// </summary>
    [Range(1, 720)]
    public int UnreadAfterHours { get; set; } = 24;

    /// <summary>How many unread messages before a member is told about them at all.</summary>
    [Range(1, 1000)]
    public int UnreadThreshold { get; set; } = 3;

    /// <summary>
    /// How long before a member may be told about unread messages again.
    /// <para>
    /// A cooldown rather than a once-ever marker, because unread is a standing state: somebody who
    /// leaves forty messages unread for a fortnight is in the same condition on day one and day
    /// fourteen, and the honest cadence is "not again for a while".
    /// </para>
    /// </summary>
    [Range(1, 8760)]
    public int UnreadCooldownHours { get; set; } = 72;

    /// <summary>How often the attention sweep runs.</summary>
    [Range(60, 86_400)]
    public int AttentionSweepIntervalSeconds { get; set; } = 3_600;

    /// <summary>How many rows one attention sweep pass reads. The bound on one cycle's work.</summary>
    [Range(1, 5_000)]
    public int AttentionSweepBatchSize { get; set; } = 200;
}
