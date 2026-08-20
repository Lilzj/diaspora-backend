namespace Diaspora.Modules.CommunityLife.Options;

/// <summary>
/// The knobs an operator can turn.
/// <para>
/// <b>Everything here is an abuse bound, a schedule or a jurisdiction decision, and nothing here is
/// a domain rule.</b> The distinction is the one <c>CommunityLifeLimits</c> states from the other
/// side: a bound an aggregate is <em>correct</em> at is a constant in the domain, because an
/// invariant that varies by deployment is not an invariant. What lives here is the set of numbers a
/// deployment genuinely might need to differ on.
/// </para>
/// </summary>
public sealed class CommunityLifeOptions
{
    public const string SectionName = "CommunityLife";

    /// <summary>
    /// Organizations one member may create.
    /// <para>
    /// Ten. Somebody genuinely runs a church, its youth group and its women's fellowship; nobody
    /// legitimately runs forty, and a member who needs to is asking for an administrator to make
    /// them one.
    /// </para>
    /// </summary>
    public int MaxOrganizationsPerCreator { get; set; } = 10;

    /// <summary>
    /// Live events one member may hold at once.
    /// <para>
    /// Generous, because a large association legitimately runs a weekly service, a monthly meeting
    /// and a season of festivals at the same time. Present only so a scripted client cannot publish
    /// four thousand overnight.
    /// </para>
    /// </summary>
    public int MaxLiveEventsPerOrganizer { get; set; } = 200;

    /// <summary>Events one member may create in a day. The burst bound, distinct from the total.</summary>
    public int MaxEventsPerOrganizerPerDay { get; set; } = 25;

    /// <summary>
    /// Live responses one member may hold across the platform.
    /// <para>
    /// Deliberately high — unlike a housing reservation, an RSVP holds nothing anybody else needs
    /// except on a capped event, and somebody active in three communities genuinely has thirty
    /// things in their diary.
    /// </para>
    /// </summary>
    public int MaxLiveRsvpsPerAttendee { get; set; } = 100;

    /// <summary>Membership requests one member may have outstanding at once.</summary>
    public int MaxPendingMembershipRequests { get; set; } = 20;

    /// <summary>
    /// Distinct reports before an event or an organization is flagged automatically.
    /// <para>
    /// Three. Fraud and impersonation bypass this and flag on the first report, because both are
    /// claims that somebody is being deceived right now and waiting for a second complaint means
    /// waiting for a second victim — see <c>CommunityLifeReport.FlagsImmediately</c>.
    /// </para>
    /// </summary>
    public int ReportsBeforeAutoFlag { get; set; } = 3;

    /// <summary>
    /// How long after an event ends before unsettled responses become attendances or no-shows.
    /// <para>
    /// Twelve hours: long enough for an organizer who ran a wedding on Saturday night to finish
    /// taking attendance on Sunday morning, short enough that the figures settle while anybody still
    /// cares about them.
    /// </para>
    /// </summary>
    public int SettleAttendanceAfterHours { get; set; } = 12;

    /// <summary>How often the lifecycle sweep runs, in minutes.</summary>
    /// <summary>
    /// Whether this deployment runs <c>CommunityLifeLifecycleSweep</c> at all.
    /// <para>
    /// On by default. The sweep is what moves an event from published to live to ended and settles
    /// attendance; a deployment without it shows events that never finish.
    /// </para>
    /// <para>
    /// <b>Added in PSR-1 Final Certification</b>, with <see cref="EnableMembershipSweep"/> — see
    /// DEBT-049. Both loops ran unconditionally in the integration suite, against the one shared
    /// SQLite connection every other sweep is switched off to protect.
    /// </para>
    /// </summary>
    public bool EnableLifecycleSweep { get; set; } = true;

    /// <summary>
    /// Whether this deployment runs <c>CommunityLifeMembershipSweep</c> at all.
    /// <para>
    /// On by default. See <see cref="EnableLifecycleSweep"/>.
    /// </para>
    /// </summary>
    public bool EnableMembershipSweep { get; set; } = true;

    public int LifecycleSweepMinutes { get; set; } = 15;

    /// <summary>How often the membership lapse sweep runs, in hours.</summary>
    public int MembershipSweepHours { get; set; } = 12;

    /// <summary>How many aggregates a single sweep pass moves. Bounds one transaction's size.</summary>
    public int SweepBatchSize { get; set; } = 200;

    /// <summary>
    /// How long a view is remembered before the same viewer counts again, in minutes.
    /// <para>
    /// Thirty. Somebody re-opening an event to check the address has not viewed it twice; somebody
    /// coming back the next day has.
    /// </para>
    /// </summary>
    public int ViewDeduplicationMinutes { get; set; } = 30;

    /// <summary>
    /// Whether a published event is automatically promoted to a waiting list when it fills.
    /// <para>
    /// On by default. The alternative to a waiting list is refusing the response outright, which
    /// loses the member and the demand signal at the same time.
    /// </para>
    /// </summary>
    public bool EnableWaitlist { get; set; } = true;

    /// <summary>
    /// Whether spam signals may hold a publication.
    /// <para>
    /// On, but the composition rule is deliberately conservative — one severe signal or two
    /// warnings. The cost of a false positive here is a church unable to announce a service.
    /// </para>
    /// </summary>
    public bool EnableSpamHold { get; set; } = true;

    /// <summary>
    /// How far ahead the community calendar will render, in days. Bounds an unpaged read.
    /// </summary>
    public int MaxCalendarWindowDays { get; set; } = 120;

    /// <summary>The radius the nearby seam answers within when a request does not say, in km.</summary>
    public double DefaultNearbyRadiusKm { get; set; } = 25;

    // ── Recurring series (B12.1) ────────────────────────────────────────────────────────────

    /// <summary>
    /// Live series one member may hold at once.
    /// <para>
    /// Far lower than the event bound, and deliberately: one series is potentially five hundred
    /// events, so the number of patterns a single member can run is the bound that actually
    /// constrains how many rows one account can create.
    /// </para>
    /// </summary>
    public int MaxLiveSeriesPerOrganizer { get; set; } = 25;

    /// <summary>
    /// How far ahead occurrences are written, in days.
    /// <para>
    /// A hundred and eighty. Long enough that a member browsing next term's calendar sees real,
    /// addressable dates they can respond to, and short enough that a weekly service does not
    /// materialise a decade of rows nobody will ever open. A rule that never ends has more dates
    /// than it has rows by design; the horizon is what makes that a bounded difference.
    /// </para>
    /// </summary>
    public int SeriesHorizonDays { get; set; } = 180;

    /// <summary>
    /// How many occurrences one expansion pass writes for one series.
    /// <para>
    /// Bounds a single transaction, and bounds the burst of publication events a newly published
    /// daily series would otherwise put on the bus in one go. What is left is picked up on the next
    /// pass.
    /// </para>
    /// </summary>
    public int MaxOccurrencesPerExpansion { get; set; } = 60;

    /// <summary>How often the series expansion sweep runs, in minutes.</summary>
    public int SeriesExpansionSweepMinutes { get; set; } = 60;

    /// <summary>
    /// Whether this deployment runs <c>CommunityLifeSeriesExpansionSweep</c> at all.
    /// <para>
    /// On by default. A deployment without it shows a recurring event whose dates stop at whatever
    /// horizon the create request happened to reach. Off in the integration suite, for the reason
    /// DEBT-049 records about every other sweep: one shared SQLite connection.
    /// </para>
    /// </summary>
    public bool EnableSeriesExpansionSweep { get; set; } = true;
}
