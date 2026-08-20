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
}
