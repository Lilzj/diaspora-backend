using System.Globalization;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Analytics.Domain.Errors;

/// <summary>
/// The module's failures, declared once.
/// <para>
/// Declared here rather than constructed at the call site so a code and its description stay
/// together, and so the wire contract cannot drift when two handlers report the same refusal.
/// </para>
/// <para>
/// <b>"Not yours" and "does not exist" are the same answer.</b> Content performance is
/// owner-scoped, and a caller who edits an identifier in a route learns nothing from a 404 that
/// they did not already know. A 403 would confirm the content exists and belongs to somebody,
/// which is precisely what an enumeration attack is looking for.
/// </para>
/// </summary>
public static class AnalyticsErrors
{
    // ── Period ──────────────────────────────────────────────────────────────────────────────

    public static readonly Error RangeIncomplete = Error.Validation(
        AnalyticsErrorCodes.RangeIncomplete,
        "A custom period needs both a start and an end date.");

    public static readonly Error RangeInverted = Error.Validation(
        AnalyticsErrorCodes.RangeInverted,
        "The start date has to be before the end date.");

    public static Error RangeTooLong(int maximumDays) => Error.Validation(
        AnalyticsErrorCodes.RangeTooLong,
        string.Create(
            CultureInfo.InvariantCulture,
            $"A custom period can cover at most {maximumDays} days."));

    public static readonly Error PeriodUnsupported = Error.Validation(
        AnalyticsErrorCodes.PeriodUnsupported,
        "That period is not one this platform reports on.");

    public static readonly Error TimezoneUnknown = Error.Validation(
        AnalyticsErrorCodes.TimezoneUnknown,
        "That is not a time zone this platform recognises.");

    // ── Authorization and existence ─────────────────────────────────────────────────────────

    public static readonly Error NotAuthenticated = Error.Unauthorized(
        AnalyticsErrorCodes.NotAuthenticated,
        "Sign in to see your analytics.");

    public static readonly Error SubjectNotFound = Error.NotFound(
        AnalyticsErrorCodes.SubjectNotFound,
        "There is nothing here to report on.");

    public static readonly Error SubjectTypeUnsupported = Error.Validation(
        AnalyticsErrorCodes.SubjectTypeUnsupported,
        "That is not something this platform reports on.");

    // ── Ingestion ───────────────────────────────────────────────────────────────────────────

    public static Error BatchInvalid(int maximumEvents) => Error.Validation(
        AnalyticsErrorCodes.BatchInvalid,
        string.Create(
            CultureInfo.InvariantCulture,
            $"A telemetry batch carries between one and {maximumEvents} events."));

    // ── Operations ──────────────────────────────────────────────────────────────────────────

    public static Error BackfillWindowTooWide(int maximumDays) => Error.Validation(
        AnalyticsErrorCodes.BackfillWindowTooWide,
        string.Create(
            CultureInfo.InvariantCulture,
            $"A backfill covers at most {maximumDays} days at a time."));

    public static readonly Error BackfillInFlight = Error.Conflict(
        AnalyticsErrorCodes.BackfillInFlight,
        "A backfill is already running. Wait for it to finish.");

    // ── Saved reports ───────────────────────────────────────────────────────────────────────

    public static readonly Error ReportNotFound = Error.NotFound(
        AnalyticsErrorCodes.ReportNotFound,
        "There is no saved report here.");

    public static Error ReportInvalid(string reason) => Error.Validation(
        AnalyticsErrorCodes.ReportInvalid, reason);

    public static readonly Error ReportSharingUnsupported = Error.Validation(
        AnalyticsErrorCodes.ReportSharingUnsupported,
        "Saved reports can only be kept private for now.");

    public static Error ReportLimitReached(int limit) => Error.Conflict(
        AnalyticsErrorCodes.ReportLimitReached,
        string.Create(
            CultureInfo.InvariantCulture,
            $"You can keep at most {limit} saved reports. Delete one to save another."));

    // ── Exports ─────────────────────────────────────────────────────────────────────────────

    public static readonly Error ExportNotFound = Error.NotFound(
        AnalyticsErrorCodes.ExportNotFound,
        "There is no export here.");

    public static Error ExportInvalid(string reason) => Error.Validation(
        AnalyticsErrorCodes.ExportInvalid, reason);

    public static readonly Error ExportFormatUnsupported = Error.Validation(
        AnalyticsErrorCodes.ExportFormatUnsupported,
        "Exports are produced as CSV. PDF and JSON are not available yet.");

    public static readonly Error ExportExpired = Error.NotFound(
        AnalyticsErrorCodes.ExportExpired,
        "This export has expired. Request a new one.");

    // ── Funnels ─────────────────────────────────────────────────────────────────────────────

    public static Error FunnelUnknown(string key) => Error.NotFound(
        AnalyticsErrorCodes.FunnelUnknown,
        $"There is no journey called \"{key}\".");

    public static readonly Error FunnelSubjectMismatch = Error.Validation(
        AnalyticsErrorCodes.FunnelSubjectMismatch,
        "That journey does not apply to this subject.");

    // ── Retention, leaderboards, insights, campaigns and premium ────────────────────────────

    /// <summary>
    /// A platform-wide figure asked for by somebody without platform analytics standing. Forbidden
    /// rather than absent: the platform's existence is not a secret, and the refusal names why.
    /// </summary>
    public static readonly Error PlatformReadRequired = Error.Forbidden(
        AnalyticsErrorCodes.PlatformReadRequired,
        "Platform-wide figures are for platform administrators.");

    /// <summary>A subject this route cannot answer for, with the reason in the message.</summary>
    public static Error SubjectUnsupportedBecause(string reason) => Error.Validation(
        AnalyticsErrorCodes.SubjectTypeUnsupported, reason);

    public static readonly Error RetentionGranularityUnsupported = Error.Validation(
        AnalyticsErrorCodes.RetentionGranularityUnsupported,
        "Cohorts are cut by day, week or month.");

    public static Error RetentionCohortCountInvalid(int maximum) => Error.Validation(
        AnalyticsErrorCodes.RetentionCohortCountInvalid,
        string.Create(CultureInfo.InvariantCulture, $"Ask for between 1 and {maximum} cohorts."));

    public static Error LeaderboardUnknown(string key) => Error.NotFound(
        AnalyticsErrorCodes.LeaderboardUnknown,
        $"There is no board called \"{key}\".");

    public static readonly Error LeaderboardSubjectMismatch = Error.Validation(
        AnalyticsErrorCodes.LeaderboardSubjectMismatch,
        "That board does not rank this subject's items.");

    public static Error LeaderboardMetricUnsupported(string reason) => Error.Validation(
        AnalyticsErrorCodes.LeaderboardMetricUnsupported, reason);

    public static readonly Error PremiumNotEntitled = Error.Forbidden(
        AnalyticsErrorCodes.PremiumNotEntitled,
        "Advanced analytics come with a plan that includes them.");
}
