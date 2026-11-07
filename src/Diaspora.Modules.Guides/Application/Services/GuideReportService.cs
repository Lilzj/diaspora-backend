using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Integration;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

// The kernel's legacy identifier bag (Diaspora.SharedKernel.Identity) also declares a GuideId.
// The module-local identifier is the authoritative one for B8; the alias resolves the
// collision without renaming a domain concept.
using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Reporting a guide, delegated to Community — never a local aggregate.
/// <para>
/// Community's reporting workflow brings the reason catalogue and the duplicate rule; a report
/// table here would be a fifth definition of "resolved" on the platform. Community sends the
/// report on to Trust as a moderation case about the guide (7 Nov 2026): staff decide there, the
/// system hides the guide pending review on many serious reports, and
/// <c>GuideModerationTarget</c> carries the decision out on the guide.
/// </para>
/// </summary>
public sealed class GuideReportService
{
    private readonly ICommunityModerationService _moderation;
    private readonly ILogger<GuideReportService> _logger;

    public GuideReportService(
        ICommunityModerationService moderation, ILogger<GuideReportService> logger)
    {
        _moderation = moderation;
        _logger = logger;
    }

    /// <summary>
    /// Files the report.
    /// <para>
    /// The reason arrives as the wire string and is parsed against Community's catalogue here,
    /// in the one class that knows Community's vocabulary. An unrecognised reason files as
    /// <c>Other</c> with the description carried verbatim — a member reporting misinformation
    /// through an older client must not be refused over a renamed enum member.
    /// </para>
    /// </summary>
    public async Task<Result> ReportAsync(
        GuideId guideId,
        UserId reporterId,
        string? reason,
        string? description = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reporterId);

        var reasonCode = ParseReason(reason);

        try
        {
            var reported = await _moderation.ReportAsync(
                GuideBookmarkService.ReferenceFor(guideId),
                reporterId,
                reasonCode,
                description,
                evidenceUrl: null,
                cancellationToken);

            return reported.IsSuccess ? Result.Success() : Result.Failure(reported.Error);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // A Community outage becomes a failed report, not an unhandled 500.
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception, "Reporting guide {GuideId} through Community failed.", guideId.Value);

            return Result.Failure(GuideErrors.ReportFailed);
        }
#pragma warning restore CA1031
    }

    private static ReportReasonCode ParseReason(string? reason) =>
        Enum.TryParse<ReportReasonCode>(reason?.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            ? parsed
            : ReportReasonCode.Other;
}
