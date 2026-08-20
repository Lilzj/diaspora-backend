namespace Diaspora.Modules.Notifications.Options;

/// <summary>
/// The knobs an operator can turn.
/// <para>
/// <b>Everything here is an abuse bound, a schedule or a retention decision, and nothing here is a
/// domain rule.</b> A bound an aggregate is <em>correct</em> at is a constant in
/// <c>NotificationLimits</c>, because an invariant that varies by deployment is not an invariant.
/// What lives here is the set of numbers a deployment genuinely might need to differ on — and, in
/// two cases, switches that let a deployment run this module without any background work at all.
/// </para>
/// </summary>
public sealed class NotificationOptions
{
    public const string SectionName = "Notifications";

    // ── Delivery ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether the dispatcher's background loop runs.
    /// <para>
    /// On in a deployment, off in the integration suite. The suite shares one SQLite connection, and
    /// a background loop querying it while a request is mid-command is a concurrent use SQLite
    /// refuses — the same reason B1.1 turns the outbox dispatcher off there. Tests drive delivery
    /// explicitly and get a deterministic result instead of one that depends on a timer.
    /// </para>
    /// </summary>
    public bool EnableDeliverySweep { get; set; } = true;

    /// <summary>How often the dispatcher looks for work.</summary>
    public int DeliverySweepSeconds { get; set; } = 15;

    /// <summary>Notifications one sweep pass will act on. Bounds the transaction, not the throughput.</summary>
    public int DeliveryBatchSize { get; set; } = 200;

    // ── Digests ─────────────────────────────────────────────────────────────────────────────

    public bool EnableDigestSweep { get; set; } = true;

    /// <summary>
    /// How often digest schedules are evaluated.
    /// <para>
    /// Hourly, matching the granularity of the schedule itself. A member asking for 8am gets it
    /// within the hour; a sweep that ran every minute would be sixty times the work to make a daily
    /// summary punctual.
    /// </para>
    /// </summary>
    public int DigestSweepMinutes { get; set; } = 60;

    /// <summary>Members whose digest is generated in one pass.</summary>
    public int DigestBatchSize { get; set; } = 500;

    // ── Retention ───────────────────────────────────────────────────────────────────────────

    public bool EnableRetentionSweep { get; set; } = true;

    public int RetentionSweepMinutes { get; set; } = 360;

    /// <summary>
    /// How long a message with no template retention lives.
    /// <para>
    /// A year. Long enough that "what did they tell me about my visa application" is answerable, and
    /// short enough that the table does not grow without limit. A template that needs different says
    /// so; a rule that needs different overrides the template.
    /// </para>
    /// </summary>
    public int DefaultRetentionDays { get; set; } = 365;

    /// <summary>Messages one retention pass expires.</summary>
    public int RetentionBatchSize { get; set; } = 1000;

    // ── Counter reconciliation ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Whether inbox counters are periodically recounted.
    /// <para>
    /// Maintained counters drift. The honest response is a recount, not a promise that they cannot —
    /// see <c>UserInbox</c>.
    /// </para>
    /// </summary>
    public bool EnableCounterReconciliation { get; set; } = true;

    public int ReconciliationSweepMinutes { get; set; } = 720;

    public int ReconciliationBatchSize { get; set; } = 250;

    // ── Abuse bounds ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Messages one member may receive on one channel inside the window.
    /// <para>
    /// Thirty an hour on push. Somebody in a busy thread legitimately generates more than that, and
    /// the surplus still lands in their inbox — what the bound protects is their lock screen.
    /// </para>
    /// </summary>
    public int MaxPerChannelPerWindow { get; set; } = 30;

    public int ThrottleWindowMinutes { get; set; } = 60;

    /// <summary>
    /// Delivery endpoints one member may hold.
    /// <para>
    /// Twenty. A person genuinely has a phone, a tablet, a laptop and a work browser; nobody
    /// legitimately has forty, and an account that accumulates them is one whose token rotation is
    /// not being detected.
    /// </para>
    /// </summary>
    public int MaxDevicesPerUser { get; set; } = 20;

    /// <summary>
    /// How long two messages from one source are treated as one, when a rule states no window.
    /// <para>
    /// At-least-once delivery guarantees a handler sees the same event twice. Without a default,
    /// every rule that forgot to set one would let the member see it twice too.
    /// </para>
    /// </summary>
    public int DefaultDeduplicationMinutes { get; set; } = 60;

    // ── Rendering ───────────────────────────────────────────────────────────────────────────

    /// <summary>How long a resolved template is cached. Cleared when one is published or retired.</summary>
    public int TemplateCacheSeconds { get; set; } = 300;

    /// <summary>The language used when a member has stated none.</summary>
    public string DefaultLanguage { get; set; } = "en";

    /// <summary>The wall clock used when a member has stated none.</summary>
    public string DefaultTimeZoneId { get; set; } = "UTC";

    // ── Unsubscribe links, B13.3 ────────────────────────────────────────────────────────────

    /// <summary>
    /// The key an email footer's unsubscribe link is signed with. No default, and none ships.
    /// <para>
    /// <b>Empty means the module generates one at startup</b>, which works, does not survive a
    /// restart and is not shared between instances — so a link minted by one pod is refused by
    /// another. That is a warning at startup rather than a failure, because a deployment that sends
    /// no email needs no key and refusing to boot over it would be refusing to boot over an unused
    /// feature.
    /// </para>
    /// <para>
    /// Comes from the platform's secret configuration like every other secret. It is not a
    /// credential for anybody else's service, but a leaked one lets somebody unsubscribe any member
    /// from any category, which is a denial of notifications rather than a disclosure.
    /// </para>
    /// </summary>
    public string? UnsubscribeSigningKey { get; set; }

    /// <summary>
    /// How long a footer's unsubscribe link keeps working, in days.
    /// <para>
    /// Ninety, because the member this link exists for is the one who has stopped opening the app —
    /// and a link that expired in a week would be dead in exactly the mailbox it was meant for. Long
    /// enough to be useful, short enough that an archived email from two years ago is not a live
    /// credential.
    /// </para>
    /// </summary>
    public int UnsubscribeLinkLifetimeDays { get; set; } = 90;
}
