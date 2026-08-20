namespace Diaspora.Modules.CommunityLife.Domain.Policies;

/// <summary>
/// The bounds the aggregates are correct at.
/// <para>
/// <b>Constants rather than options, and the distinction is the one <c>CommunityLifeOptions</c>
/// states from the other side.</b> A bound an aggregate enforces to stay <em>correct</em> is an
/// invariant, and an invariant that varies by deployment is not an invariant — an event with forty
/// agenda items on one deployment and twelve on another is two different contracts with the same
/// wire shape. What belongs in options is the abuse ceilings and the sweep schedules, which
/// genuinely do differ. B10 recorded the split, B11 kept it, and so does this.
/// </para>
/// </summary>
public static class CommunityLifeLimits
{
    // ── Shared text ─────────────────────────────────────────────────────────────────────────

    public const int MinTitle = 3;

    public const int MaxTitle = 200;

    public const int MaxSummary = 400;

    public const int MaxDescription = 20_000;

    public const int MaxSlug = 220;

    public const int MaxCategoryKey = 80;

    public const int MaxDisplayName = 200;

    /// <summary>A short free-text line: a caption, a note, a decline reason.</summary>
    public const int MaxNoteLength = 1_000;

    public const int MaxModerationNote = 2_000;

    public const int MaxModerationNotes = 100;

    public const int MaxLocationName = 200;

    /// <summary>B4's materialised path, stored verbatim so scope narrowing is an indexed prefix.</summary>
    public const int MaxAreaPath = 512;

    public const int MaxUrl = 500;

    // ── Organization ────────────────────────────────────────────────────────────────────────

    public const int MaxMission = 4_000;

    public const int MaxVision = 4_000;

    public const int MaxMembershipRequirements = 4_000;

    /// <summary>
    /// Named leaders on an organization page. Twelve covers a church council or a committee, and
    /// stops the leadership list becoming an unbounded member directory rendered on every card.
    /// </summary>
    public const int MaxLeaders = 12;

    /// <summary>Languages a page claims. Ten covers a genuinely multilingual congregation.</summary>
    public const int MaxLanguages = 10;

    public const int MaxLanguageName = 60;

    /// <summary>
    /// Operating-hour rows. Sixteen is eight days — the week plus public holidays — times two, so
    /// a place that opens twice a day can say so.
    /// </summary>
    public const int MaxOperatingHours = 16;

    /// <summary>Named services an organization offers. Rendered as chips.</summary>
    public const int MaxServices = 25;

    public const int MaxServiceName = 120;

    // ── Event ───────────────────────────────────────────────────────────────────────────────

    public const int MaxAgendaItems = 40;

    public const int MaxAgendaTitle = 200;

    public const int MaxSpeakers = 40;

    public const int MaxAccessibilityNotes = 2_000;

    public const int MaxDressCode = 200;

    /// <summary>
    /// How far ahead an event may be scheduled. Five years is generous for a real occasion and
    /// short enough that a mistyped year is refused rather than filed.
    /// </summary>
    public const int MaxScheduleHorizonDays = 365 * 5;

    /// <summary>
    /// The longest a single event may run. Thirty days covers a festival season; anything longer
    /// is a programme of events, which is a series rather than one occasion.
    /// </summary>
    public const int MaxDurationDays = 30;

    // ── Recurring series ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The largest <c>interval</c> a rule may carry. Fifty-two makes "once a year" expressible as a
    /// weekly rule, which somebody will do, and stops an interval of ten thousand walking the
    /// expander four hundred periods into the year 12,000.
    /// </summary>
    public const int MaxRecurrenceInterval = 52;

    /// <summary>
    /// The most occurrences one series may ever hold.
    /// <para>
    /// Five hundred is ten years of a weekly service or forty of a monthly meeting, and it is the
    /// ceiling on both the <c>count</c> a rule may name and the number of events one series may
    /// materialise. It exists because a series is the one write on this platform that turns a single
    /// request into an unbounded number of rows.
    /// </para>
    /// </summary>
    public const int MaxSeriesOccurrences = 500;

    /// <summary>
    /// Removed dates one series may carry. One per cancelled or moved occurrence; a series with
    /// more exceptions than this is not a pattern any more.
    /// </summary>
    public const int MaxSeriesExceptions = 200;

    public const int MaxExceptionReason = 64;

    // ── References ──────────────────────────────────────────────────────────────────────────

    public const int MaxMediaReferences = 40;

    public const int MaxGuideReferences = 10;

    public const int MaxProviderReferences = 5;

    public const int MaxLinkedResources = 50;

    // ── Paging ──────────────────────────────────────────────────────────────────────────────

    public const int DefaultPageSize = 20;

    public const int MaxPageSize = 100;
}
