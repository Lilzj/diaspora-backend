using System.ComponentModel.DataAnnotations;

namespace Diaspora.Modules.Automation.Options;

/// <summary>
/// The ceilings and horizons a deployment sets.
/// <para>
/// <b>Every one has a default that is safe rather than generous.</b> An automation platform's failure
/// mode is volume — a rule that notifies too often, a chain that never ends, a broadcast that reaches
/// more people than the moderator expected — and a default chosen for convenience is a default that
/// makes the first incident bigger.
/// </para>
/// </summary>
public sealed class AutomationOptions
{
    public const string SectionName = "AutomationDefinition";

    /// <summary>
    /// The most active automations one owner may hold.
    /// <para>
    /// Reported by the capability endpoint so the client can say so <i>before</i> a member hits it,
    /// which is the difference between a limit and a rejection.
    /// </para>
    /// </summary>
    [Range(1, 500)]
    public int MaximumActiveAutomationsPerOwner { get; set; } = 50;

    /// <summary>
    /// How deep a chain of automations may go before the engine refuses.
    /// <para>
    /// <b>Three, and the number is deliberately small.</b> An automation reacting to another
    /// automation's effect is legitimate once; twice is unusual; three times is almost always two
    /// rules that have found each other. The ceiling turns an infinite chain into a bounded one with a
    /// recorded refusal, which is what makes runaway automation an incident somebody can read rather
    /// than an outage somebody has to diagnose.
    /// </para>
    /// </summary>
    [Range(1, 10)]
    public int MaximumChainDepth { get; set; } = 3;

    /// <summary>
    /// The most runs one automation may start in <see cref="RateWindowMinutes"/>.
    /// <para>
    /// A per-automation ceiling rather than only a per-owner one, because the failure it prevents is a
    /// single rule matching a burst of events — a marketplace importing a thousand listings should not
    /// produce a thousand notifications from one saved search.
    /// </para>
    /// </summary>
    [Range(1, 10_000)]
    public int MaximumRunsPerAutomationPerWindow { get; set; } = 60;

    /// <summary>The most runs one owner may start across every automation in the same window.</summary>
    [Range(1, 100_000)]
    public int MaximumRunsPerOwnerPerWindow { get; set; } = 300;

    [Range(1, 1440)]
    public int RateWindowMinutes { get; set; } = 60;

    /// <summary>
    /// The most people one broadcast may reach.
    /// <para>
    /// Exceeding it refuses the action with a recorded reason rather than truncating it. A truncated
    /// broadcast reads to a moderator as a delivered one, and the members who were not reached have no
    /// way to know they were not.
    /// </para>
    /// </summary>
    [Range(1, 100_000)]
    public int MaximumBroadcastRecipients { get; set; } = 5_000;

    [Range(10, 1_000)]
    public int BroadcastPageSize { get; set; } = 200;

    /// <summary>
    /// How late a due occurrence may be and still fire.
    /// <para>
    /// The catch-up policy, as a number. Past this it is skipped with a record: a daily reminder that
    /// arrives eleven hours late is worse than one that does not arrive, and the member needs to be
    /// able to see that it was skipped and why.
    /// </para>
    /// </summary>
    [Range(1, 1440)]
    public int OccurrenceCatchUpMinutes { get; set; } = 60;

    /// <summary>How long a claimed occurrence is held before another instance may take it.</summary>
    [Range(1, 60)]
    public int OccurrenceClaimMinutes { get; set; } = 5;

    /// <summary>How many times a due occurrence is retried before it is dead-lettered.</summary>
    [Range(1, 20)]
    public int MaximumOccurrenceAttempts { get; set; } = 5;

    /// <summary>How often the sweeper looks for due work.</summary>
    [Range(5, 3600)]
    public int SweepIntervalSeconds { get; set; } = 30;

    /// <summary>How many due occurrences one sweep claims.</summary>
    [Range(1, 1000)]
    public int SweepBatchSize { get; set; } = 100;

    /// <summary>
    /// How long a run may stay <c>Running</c> before the sweeper times it out.
    /// <para>
    /// A run whose notification request was published and whose settlement never arrived would
    /// otherwise sit unresolved forever, and a member reading history would see an automation
    /// permanently mid-flight.
    /// </para>
    /// </summary>
    [Range(1, 1440)]
    public int RunTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// How long execution history is kept.
    /// <para>
    /// Finite and stated, because an execution log over a location or a wallet trigger is a history of
    /// somebody's movements and money. Ninety days is long enough to diagnose a complaint and short
    /// enough that it is not an archive.
    /// </para>
    /// <para>
    /// <b>B24's diagnostics route clamps its window to ninety days as a constant of its own, and the
    /// two are deliberately allowed to differ.</b> This setting decides how long history is kept; that
    /// one decides the widest question a caller may ask. A deployment that shortens this would
    /// otherwise silently shorten the widest window a member can ask for, and one that lengthens it
    /// would widen that route's cost without anybody choosing to. What they must not do is drift
    /// apart unnoticed — which is what this paragraph and its counterpart in
    /// <c>GetAutomationDiagnosticsQueryHandler</c> exist to prevent.
    /// </para>
    /// </summary>
    [Range(7, 3650)]
    public int RunRetentionDays { get; set; } = 90;

    /// <summary>
    /// How long before a dated subject's own moment the platform looks at it — Wave 12.
    /// <para>
    /// <b>The platform's lead, not the member's.</b> A member's "how far before" is the
    /// <c>reminder.schedule</c> action's <c>offsetMinutes</c>, and it counts back from the subject's
    /// moment independently of this. This is when the <i>trigger</i> fires — when
    /// <c>job.closingSoon</c>, <c>listing.expiring</c>, <c>listing.reservationEnding</c> and
    /// <c>housing.viewingReminder</c> become true — and the frontend's own wording for all four is "a
    /// set time before", with no field for the member to choose one.
    /// </para>
    /// <para>
    /// Twenty-four hours because that is the shortest lead that is still actionable for all four: it is
    /// long enough to apply for a job, renew a listing, chase a reservation or rearrange a viewing, and
    /// short enough that the notification still reads as urgent.
    /// </para>
    /// </summary>
    [Range(1, 720)]
    public int SubjectHorizonLeadHours { get; set; } = 24;

    /// <summary>
    /// How long a settled horizon row is kept.
    /// <para>
    /// Shorter than run retention, because a horizon holds no history a member would ever read — it is
    /// working state, and the run it produced is the durable record.
    /// </para>
    /// </summary>
    [Range(1, 365)]
    public int SubjectHorizonRetentionDays { get; set; } = 14;

    /// <summary>The largest a trigger or action parameter bag may be, in characters.</summary>
    [Range(64, 16_384)]
    public int MaximumParameterLength { get; set; } = 2_048;

    /// <summary>The most conditions and actions one automation may carry.</summary>
    [Range(1, 100)]
    public int MaximumConditionsPerAutomation { get; set; } = 20;

    [Range(1, 50)]
    public int MaximumActionsPerAutomation { get; set; } = 10;

    /// <summary>
    /// Counts below this are banded rather than reported exactly.
    /// <para>
    /// "3 members match" in a small community identifies people. The same suppression rule B19 applies
    /// to small analytics cohorts, applied to an audience estimate for the same reason.
    /// </para>
    /// </summary>
    [Range(2, 100)]
    public int AudienceSuppressionThreshold { get; set; } = 10;

    /// <summary>The most simulations one member may run in an hour. A dry run reads authoritative state.</summary>
    [Range(1, 1000)]
    public int MaximumSimulationsPerHour { get; set; } = 60;
}
