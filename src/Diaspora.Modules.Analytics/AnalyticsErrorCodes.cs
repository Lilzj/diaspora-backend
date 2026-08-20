namespace Diaspora.Modules.Analytics;

/// <summary>
/// The wire codes this module returns.
/// <para>
/// Dotted, module-prefixed, and stable: a client switches on these, so a rename is a breaking
/// change.
/// </para>
/// <para>
/// <b>Two of them carry the <c>creator.</c> prefix, and that is not a slip.</b>
/// <see cref="RangeIncomplete"/> and <see cref="RangeInverted"/> are quoted from the Phase 7
/// register — "the adapter already refuses an incomplete range (<c>creator.analytics.range_incomplete</c>)
/// and an inverted one (<c>creator.analytics.range_inverted</c>) rather than silently swapping the
/// dates" — and the client's period selector already handles exactly those two strings. Renaming
/// them to fit this module's prefix would have been a client change for no gain.
/// </para>
/// <para>
/// <b>Both are returned as 400, not 422.</b> The frontend's error mapper folds 400 and 422 into one
/// <c>validation</c> kind and switches on the code, so the two are indistinguishable to it; the
/// platform's <c>ErrorType.Validation</c> is 400 everywhere else, and one module answering 422
/// would be the inconsistency the API style guide exists to prevent.
/// </para>
/// </summary>
public static class AnalyticsErrorCodes
{
    private const string Prefix = "analytics.";

    // ── Range and period — the client's own codes ───────────────────────────────────────────

    /// <summary>A custom period arrived with only one bound.</summary>
    public const string RangeIncomplete = "creator.analytics.range_incomplete";

    /// <summary>A custom period arrived with its start after its end.</summary>
    public const string RangeInverted = "creator.analytics.range_inverted";

    /// <summary>The period is longer than this deployment retains data for.</summary>
    public const string RangeTooLong = Prefix + "range.too-long";

    /// <summary>The period preset is not one this subject supports.</summary>
    public const string PeriodUnsupported = Prefix + "period.unsupported";

    /// <summary>The timezone is not a name this deployment recognises.</summary>
    public const string TimezoneUnknown = Prefix + "timezone.unknown";

    // ── Authorization and existence ─────────────────────────────────────────────────────────

    public const string NotAuthenticated = Prefix + "not-authenticated";

    /// <summary>Nothing analytics has ever seen carries this identifier.</summary>
    public const string SubjectNotFound = Prefix + "subject.not-found";

    /// <summary>The subject type is not one this deployment serves analytics for.</summary>
    public const string SubjectTypeUnsupported = Prefix + "subject.unsupported";

    // ── Ingestion ───────────────────────────────────────────────────────────────────────────

    /// <summary>The batch carried no events, or more than the cap.</summary>
    public const string BatchInvalid = Prefix + "ingest.batch-invalid";

    /// <summary>The event name is not in the schema registry.</summary>
    public const string EventTypeUnknown = Prefix + "ingest.event-unknown";

    /// <summary>The event name is registered but not at the version submitted.</summary>
    public const string EventVersionUnknown = Prefix + "ingest.event-version-unknown";

    /// <summary>The event is registered but is server-produced; a client may not submit it.</summary>
    public const string EventNotClientSubmittable = Prefix + "ingest.event-not-submittable";

    /// <summary>The payload was malformed, oversized, or carried a value of the wrong shape.</summary>
    public const string EventMalformed = Prefix + "ingest.event-malformed";

    /// <summary>The event carried a property the registry does not allow for it.</summary>
    public const string PropertyNotAllowed = Prefix + "ingest.property-not-allowed";

    /// <summary><c>occurredAt</c> was outside the window this deployment accepts.</summary>
    public const string OccurredAtOutOfRange = Prefix + "ingest.occurred-at-out-of-range";

    /// <summary>The same event identity was already accepted. Not a failure — reported per event.</summary>
    public const string Duplicate = Prefix + "ingest.duplicate";

    /// <summary>
    /// The caller is the owner of the thing they reported looking at.
    /// <para>
    /// Its own code rather than <see cref="Duplicate"/>, because a duplicate is a statement that the
    /// event was already stored and this one never will be. A client that logged the two the same way
    /// would be unable to tell "you already sent this" from "we do not count you".
    /// </para>
    /// </summary>
    public const string SelfExcluded = Prefix + "ingest.self-excluded";

    // ── Operations ──────────────────────────────────────────────────────────────────────────

    /// <summary>A backfill was requested over a window wider than the operation permits.</summary>
    public const string BackfillWindowTooWide = Prefix + "backfill.window-too-wide";

    /// <summary>A backfill is already running. One at a time, by design.</summary>
    public const string BackfillInFlight = Prefix + "backfill.in-flight";
}
