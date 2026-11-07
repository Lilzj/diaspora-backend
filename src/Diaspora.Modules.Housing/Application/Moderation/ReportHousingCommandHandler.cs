using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Moderation;

/// <summary>
/// Records a report against a property.
/// <para>
/// <b>A scam allegation flags the listing on the first report; everything else waits for the
/// configured threshold.</b> That asymmetry is the module's central safety judgement. Waiting for a
/// second reporter on a rental advance-fee fraud means letting it run for another day, and a day is
/// enough for somebody who has not yet arrived in the country to send a deposit they will never see
/// again. Waiting on every other reason is what stops a competing landlord using the report button
/// as a takedown tool.
/// </para>
/// <para>
/// <b>One report per member per listing, enforced by a unique index.</b> Without it a single member
/// could drive any listing past the threshold alone.
/// </para>
/// </summary>
public sealed class ReportHousingCommandHandler : ICommandHandler<ReportHousingCommand, Guid>
{
    private readonly IHousingListingRepository _listings;
    private readonly IHousingReportRepository _reports;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IOptions<HousingOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ReportHousingCommandHandler(
        IHousingListingRepository listings,
        IHousingReportRepository reports,
        IHousingAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IOptions<HousingOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _reports = reports;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(
        ReportHousingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<Guid>(caller.Error);
        }

        var listing = await _listings.GetAsync(
            HousingListingId.From(command.ListingId), cancellationToken);

        if (listing is null || !await _authorization.CanSeeAsync(listing, cancellationToken))
        {
            return Result.Failure<Guid>(HousingErrors.ListingNotFound);
        }

        if (await _reports.HasReportedAsync(listing.Id, caller.Value, cancellationToken))
        {
            return Result.Failure<Guid>(HousingErrors.ReportAlreadyFiled);
        }

        if (!Enum.TryParse<HousingReportReason>(
                command.Request.Reason, ignoreCase: true, out var reason))
        {
            return Result.Failure<Guid>(HousingErrors.ReportReasonInvalid);
        }

        var now = _clock.UtcNow;

        // Including this one: the number the published report contract carries.
        var total = await _reports.CountForListingAsync(listing.Id, cancellationToken) + 1;

        var report = HousingReport.File(
            HousingReportId.From(_guids.NewSequentialGuid()),
            listing.Id,
            listing.LandlordId,
            caller.Value,
            reason,
            command.Request.Detail,
            total,
            now);

        if (report.IsFailure)
        {
            return Result.Failure<Guid>(report.Error);
        }

        await _reports.AddAsync(report.Value, cancellationToken);

        // A scam flags on the first; everything else waits. See the class summary.
        var threshold = report.Value.FlagsImmediately
            ? 1
            : _options.Value.ReportsBeforeAutoFlag;

        // The flag is the system's act on a count of reports, not the reporter's: recording the
        // reporter as the one who changed the listing's moderation put a member's name on a
        // staff-style action (7 Nov 2026).
        listing.RecordReport(threshold, now, reportedBy: null);

        var analytics = await _analytics.GetByListingAsync(listing.Id, cancellationToken);

        analytics?.RecordReport(now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        HousingTelemetry.ListingsReported.Add(
            1, new KeyValuePair<string, object?>("reason", reason.ToString()));

        return Result.Success(report.Value.Id.Value);
    }
}
