using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Reports;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>
/// Resolves a report, and every other open report on the same listing with it.
/// <para>
/// <b>Resolving one and leaving four open would put the same listing back at the top of the queue
/// four more times.</b> A moderator looks at a listing, not at a report — so the decision applies to
/// every allegation outstanding against it, and each reporter's row records the same outcome.
/// </para>
/// <para>
/// Dismissing every report also clears the automatic flag, because the listing has been looked at
/// and found to be nothing. Upholding leaves the moderation state exactly where the moderator's own
/// action put it.
/// </para>
/// </summary>
public sealed class ResolveReportCommandHandler : ICommandHandler<ResolveReportCommand>
{
    private readonly IListingReportRepository _reports;
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IClock _clock;

    public ResolveReportCommandHandler(
        IListingReportRepository reports,
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IClock clock)
    {
        _reports = reports;
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result> Handle(
        ResolveReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        if (!_authorization.IsStaff)
        {
            return Result.Failure(MarketplaceErrors.NotModerator);
        }

        var outcome = MarketplaceWireNames.ParseReportStatus(command.Request.Outcome);

        if (outcome is not (ListingReportStatus.Actioned or ListingReportStatus.Dismissed))
        {
            return Result.Failure(MarketplaceErrors.ReportReasonInvalid);
        }

        var report = await _reports.GetAsync(
            ListingReportId.From(command.ReportId), cancellationToken);

        if (report is null)
        {
            return Result.Failure(MarketplaceErrors.ReportNotFound);
        }

        var now = _clock.UtcNow;

        var open = await _reports.GetOpenForListingAsync(report.ListingId, cancellationToken);

        foreach (var sibling in open)
        {
            sibling.Resolve(outcome.Value, caller.Value, command.Request.Resolution, now);
        }

        // Dismissing means the listing was looked at and found to be nothing. Leaving it flagged
        // would put it back at the top of the queue tomorrow.
        if (outcome == ListingReportStatus.Dismissed)
        {
            var listing = await _listings.GetAsync(report.ListingId, cancellationToken);

            if (listing?.ModerationState == ListingModerationState.Flagged)
            {
                listing.Unhide(caller.Value, now);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
