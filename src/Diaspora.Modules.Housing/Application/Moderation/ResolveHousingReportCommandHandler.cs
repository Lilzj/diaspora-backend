using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Moderation;

/// <summary>
/// Closes a report.
/// <para>
/// <b>Upholding and dismissing are recorded distinctly, and both matter downstream.</b> A landlord
/// with three upheld reports is a different proposition from one with three dismissed ones, and the
/// fraud seam reads exactly that difference — a queue that only recorded "closed" would throw away
/// the signal it exists to produce.
/// </para>
/// <para>
/// <b>Dismissing clears the automatic flag.</b> A listing flagged by the threshold and then found
/// to be fine should go back into discovery, and leaving it flagged would quietly penalise a
/// landlord who did nothing wrong.
/// </para>
/// </summary>
public sealed class ResolveHousingReportCommandHandler
    : ICommandHandler<ResolveHousingReportCommand, HousingReportResponse>
{
    private readonly IHousingReportRepository _reports;
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public ResolveHousingReportCommandHandler(
        IHousingReportRepository reports,
        IHousingListingRepository listings,
        ILandlordProfileRepository landlords,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _reports = reports;
        _listings = listings;
        _landlords = landlords;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<HousingReportResponse>> Handle(
        ResolveHousingReportCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<HousingReportResponse>(caller.Error);
        }

        var report = await _reports.GetAsync(
            HousingReportId.From(command.ReportId), cancellationToken);

        if (report is null)
        {
            return Result.Failure<HousingReportResponse>(HousingErrors.ReportNotFound);
        }

        var now = _clock.UtcNow;

        var resolved = report.Resolve(
            caller.Value, command.Request.Upheld, command.Request.Resolution, now);

        if (resolved.IsFailure)
        {
            return Result.Failure<HousingReportResponse>(resolved.Error);
        }

        var listing = await _listings.GetIncludingDeletedAsync(
            report.ListingId, cancellationToken);

        ApplyToListing(listing, command, caller.Value, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var landlord = await _landlords.GetForReadAsync(report.LandlordId, cancellationToken);

        var upheld = await _reports.CountUpheldForLandlordAsync(
            report.LandlordId, cancellationToken);

        return Result.Success(
            HousingReportMapper.ToResponse(report, listing, landlord, upheld));
    }

    /// <summary>
    /// Carries the decision through to the property.
    /// <para>
    /// Upholding may hide it; dismissing clears an automatic flag. Both write a note, because a
    /// moderation decision with no recorded reason is one nobody can review later.
    /// </para>
    /// </summary>
    private void ApplyToListing(
        HousingListing? listing,
        ResolveHousingReportCommand command,
        SharedKernel.Identity.UserId moderatorId,
        DateTimeOffset now)
    {
        if (listing is null)
        {
            return;
        }

        if (command.Request.Upheld && command.Request.HideListing)
        {
            listing.Hide(command.Request.Resolution, moderatorId, now);
        }
        else if (!command.Request.Upheld)
        {
            // Found to be fine. Back into discovery. See the class summary.
            listing.Clear(moderatorId, now);
        }

        var note = command.Request.Resolution
            ?? (command.Request.Upheld ? "Report upheld." : "Report dismissed.");

        listing.AddModerationNote(_guids.NewSequentialGuid(), moderatorId, note, now);
    }
}
