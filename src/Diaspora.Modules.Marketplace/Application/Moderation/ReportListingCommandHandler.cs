using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Listings;
using Diaspora.Modules.Marketplace.Domain.Reports;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>
/// Files a report against a listing.
/// <para>
/// <b>One report per member per listing.</b> A member who reports the same listing twice amends
/// their own report rather than inflating the count that decides how urgently a moderator looks —
/// which is the number a determined individual could otherwise drive to fifty on their own.
/// </para>
/// <para>
/// <b>Nothing here hides anything.</b> The flag decision belongs to the handler for the event this
/// raises, where the threshold and the fraud exception both live.
/// </para>
/// </summary>
public sealed class ReportListingCommandHandler : ICommandHandler<ReportListingCommand>
{
    private readonly IListingReportRepository _reports;
    private readonly IMarketplaceListingRepository _listings;
    private readonly IUnitOfWork _unitOfWork;
    private readonly MarketplaceAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ReportListingCommandHandler(
        IListingReportRepository reports,
        IMarketplaceListingRepository listings,
        IUnitOfWork unitOfWork,
        MarketplaceAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _reports = reports;
        _listings = listings;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result> Handle(
        ReportListingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var reason = MarketplaceWireNames.ParseReportReason(command.Request.Reason);

        if (reason is null)
        {
            return Result.Failure(MarketplaceErrors.ReportReasonInvalid);
        }

        var listingId = MarketplaceListingId.From(command.ListingId);
        var listing = await _listings.GetAsync(listingId, cancellationToken);

        if (listing is null || !await _authorization.CanSeeAsync(listing, cancellationToken))
        {
            return Result.Failure(MarketplaceErrors.ListingNotFound);
        }

        var existing = await _reports.FindAsync(listingId, caller.Value, cancellationToken);

        if (existing is not null)
        {
            // Their own report, amended. The count does not move, which is the point.
            var amended = existing.AmendDetail(reason.Value, command.Request.Detail);

            if (amended.IsFailure)
            {
                return amended;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }

        // The count including this one, so the event carries the number a triage rule needs.
        var total = await _reports.CountForListingAsync(listingId, cancellationToken) + 1;

        var report = ListingReport.File(
            ListingReportId.From(_guids.NewSequentialGuid()),
            listingId,
            listing.SellerId,
            caller.Value,
            reason.Value,
            command.Request.Detail,
            total,
            _clock.UtcNow);

        if (report.IsFailure)
        {
            return Result.Failure(report.Error);
        }

        await _reports.AddAsync(report.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
