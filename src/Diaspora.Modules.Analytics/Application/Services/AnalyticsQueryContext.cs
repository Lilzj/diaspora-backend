using Diaspora.Modules.Analytics.Domain.Enums;
using Diaspora.Modules.Analytics.Domain.Errors;
using Diaspora.Modules.Analytics.Domain.Periods;
using Diaspora.Modules.Analytics.Domain.Repositories;
using Diaspora.Modules.Analytics.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Analytics.Application.Services;

/// <summary>
/// The one door to "who is calling, what may they see, and over what window".
/// <para>
/// <b>Every read in this module goes through it, so the answer is one answer rather than six.</b>
/// The register names the alternative precisely: an owner-scoped route whose subject identifier is
/// not checked against the caller on every request is "the single most likely place for an IDOR in
/// the phase". Concentrating the check is what makes it impossible for one endpoint to have a habit
/// the others do not.
/// </para>
/// </summary>
public sealed class AnalyticsQueryContext
{
    private readonly ICurrentUser _currentUser;
    private readonly IAnalyticsContentDimensionRepository _dimensions;
    private readonly AnalyticsReportingCalendar _calendar;
    private readonly AnalyticsOptions _options;

    public AnalyticsQueryContext(
        ICurrentUser currentUser,
        IAnalyticsContentDimensionRepository dimensions,
        AnalyticsReportingCalendar calendar,
        IOptions<AnalyticsOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _currentUser = currentUser;
        _dimensions = dimensions;
        _calendar = calendar;
        _options = options.Value;
    }

    public AnalyticsReportingCalendar Calendar => _calendar;

    /// <summary>Whether the caller may read analytics about a subject they do not own.</summary>
    public bool IsPlatformReader =>
        _currentUser.Permissions.Contains(AnalyticsPermissions.PlatformRead, StringComparer.Ordinal);

    /// <summary>The caller, or a refusal.</summary>
    public Result<Guid> RequireUser() =>
        _currentUser.IsAuthenticated && _currentUser.UserId is { } userId
            ? Result.Success(userId.Value)
            : Result.Failure<Guid>(AnalyticsErrors.NotAuthenticated);

    /// <summary>
    /// Resolves the requested window.
    /// <para>
    /// The timezone is the deployment's unless the caller names one this host knows. A caller-supplied
    /// zone is honoured because a member reading their own figures in Lagos reasonably wants Lagos
    /// days; an <em>unknown</em> zone is refused rather than quietly replaced, because silently
    /// answering in a different calendar than the one asked for is the failure §58 exists to prevent.
    /// </para>
    /// </summary>
    public Result<AnalyticsPeriod> ResolvePeriod(string? period, string? from, string? to, string? timezone)
    {
        var preset = ParsePreset(period);

        if (preset is null)
        {
            return Result.Failure<AnalyticsPeriod>(AnalyticsErrors.PeriodUnsupported);
        }

        TimeZoneInfo zone;

        if (string.IsNullOrWhiteSpace(timezone))
        {
            zone = _calendar.Zone;
        }
        else
        {
            var requested = AnalyticsReportingCalendar.TryResolve(timezone);

            if (requested is null)
            {
                return Result.Failure<AnalyticsPeriod>(AnalyticsErrors.TimezoneUnknown);
            }

            zone = requested;
        }

        var parsedFrom = ParseDate(from);
        var parsedTo = ParseDate(to);

        // A malformed date on a custom period is an incomplete range rather than a separate error:
        // the client's own refusal vocabulary has two codes and this is the one it renders.
        if (preset == AnalyticsPeriodPreset.Custom
            && ((from is not null && parsedFrom is null) || (to is not null && parsedTo is null)))
        {
            return Result.Failure<AnalyticsPeriod>(AnalyticsErrors.RangeIncomplete);
        }

        var resolution = AnalyticsPeriod.Resolve(
            preset.Value,
            parsedFrom,
            parsedTo,
            zone,
            _calendar.UtcNow,
            _options.MaximumCustomRangeDays);

        return resolution.Failure switch
        {
            AnalyticsPeriodFailure.None => Result.Success(resolution.Period!),
            AnalyticsPeriodFailure.Incomplete =>
                Result.Failure<AnalyticsPeriod>(AnalyticsErrors.RangeIncomplete),
            AnalyticsPeriodFailure.Inverted =>
                Result.Failure<AnalyticsPeriod>(AnalyticsErrors.RangeInverted),
            AnalyticsPeriodFailure.TooLong =>
                Result.Failure<AnalyticsPeriod>(AnalyticsErrors.RangeTooLong(resolution.Limit)),
            _ => Result.Failure<AnalyticsPeriod>(AnalyticsErrors.PeriodUnsupported)
        };
    }

    /// <summary>
    /// Confirms the caller may read one item's performance.
    /// <para>
    /// <b>"Not yours" and "does not exist" are the same answer.</b> A 403 would confirm the item
    /// exists and belongs to somebody, which is precisely what an enumeration attack is looking for.
    /// A platform reader is exempt, which is the only way a subject the caller does not own is ever
    /// readable.
    /// </para>
    /// </summary>
    public async Task<Result<Guid>> RequireContentOwnerAsync(
        AnalyticsContentType contentType, Guid contentId, CancellationToken cancellationToken)
    {
        var caller = RequireUser();

        if (caller.IsFailure)
        {
            return caller;
        }

        if (contentType == AnalyticsContentType.Unknown || contentId == Guid.Empty)
        {
            return Result.Failure<Guid>(AnalyticsErrors.SubjectTypeUnsupported);
        }

        var dimension = await _dimensions
            .FindAsync(contentType, contentId, cancellationToken)
            .ConfigureAwait(false);

        if (dimension is null)
        {
            return Result.Failure<Guid>(AnalyticsErrors.SubjectNotFound);
        }

        if (dimension.OwnerUserId != caller.Value && !IsPlatformReader)
        {
            return Result.Failure<Guid>(AnalyticsErrors.SubjectNotFound);
        }

        return Result.Success(dimension.OwnerUserId);
    }

    /// <summary>Reads the client's period vocabulary. Null when it is not one of the six.</summary>
    public static AnalyticsPeriodPreset? ParsePreset(string? period) =>
        period?.Trim().ToLowerInvariant() switch
        {
            null or "" or "30d" or "last30d" => AnalyticsPeriodPreset.Last30Days,
            "today" => AnalyticsPeriodPreset.Today,
            "7d" or "last7d" => AnalyticsPeriodPreset.Last7Days,
            "90d" or "last90d" => AnalyticsPeriodPreset.Last90Days,
            "year" or "thisyear" => AnalyticsPeriodPreset.Year,
            "custom" => AnalyticsPeriodPreset.Custom,
            _ => null
        };

    /// <summary>The client's own spelling for a preset, echoed back on the response.</summary>
    public static string ToClientPeriod(AnalyticsPeriodPreset preset) => preset switch
    {
        AnalyticsPeriodPreset.Today => "today",
        AnalyticsPeriodPreset.Last7Days => "7d",
        AnalyticsPeriodPreset.Last30Days => "30d",
        AnalyticsPeriodPreset.Last90Days => "90d",
        AnalyticsPeriodPreset.Year => "year",
        _ => "custom"
    };

    /// <summary>The Phase 15 preset name for the same window.</summary>
    public static string ToDatasetPreset(AnalyticsPeriodPreset preset) => preset switch
    {
        AnalyticsPeriodPreset.Today => "today",
        AnalyticsPeriodPreset.Last7Days => "last7d",
        AnalyticsPeriodPreset.Last30Days => "last30d",
        AnalyticsPeriodPreset.Last90Days => "last90d",
        AnalyticsPeriodPreset.Year => "thisYear",
        _ => "custom"
    };

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(
            value,
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None,
            out var parsed)
            ? parsed
            : null;
}
