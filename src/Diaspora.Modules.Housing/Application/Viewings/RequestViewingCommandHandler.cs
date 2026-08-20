using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Viewings;

/// <summary>
/// Asks to see a property.
/// <para>
/// <b>The open-viewing ceiling is the check that matters most here, and not for the usual
/// reason.</b> Elsewhere an abuse bound stops somebody flooding a queue; here it stops somebody
/// collecting addresses. A confirmed viewing discloses a street, so a member who could hold two
/// hundred open requests could map the empty homes in a city — which is the reconnaissance step of
/// a real crime rather than a hypothetical one.
/// </para>
/// <para>
/// <b>Requesting a viewing does not hold a unit</b>, and that asymmetry with reservations is the
/// whole design: ten people may hold a viewing on the same flat on the same Saturday, and the
/// property stays on the market throughout. It is precisely because viewings are cheap that
/// reservations can afford to be expensive.
/// </para>
/// </summary>
public sealed class RequestViewingCommandHandler
    : ICommandHandler<RequestViewingCommand, HousingViewingResponse>
{
    private readonly IHousingListingRepository _listings;
    private readonly IHousingViewingRepository _viewings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IOptions<HousingOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RequestViewingCommandHandler(
        IHousingListingRepository listings,
        IHousingViewingRepository viewings,
        ILandlordProfileRepository landlords,
        IHousingAnalyticsRepository analytics,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IOptions<HousingOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _viewings = viewings;
        _landlords = landlords;
        _analytics = analytics;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<HousingViewingResponse>> Handle(
        RequestViewingCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<HousingViewingResponse>(caller.Error);
        }

        var listing = await _listings.GetAsync(
            HousingListingId.From(command.ListingId), cancellationToken);

        if (listing is null || !await _authorization.CanSeeAsync(listing, cancellationToken))
        {
            return Result.Failure<HousingViewingResponse>(HousingErrors.ListingNotFound);
        }

        if (!listing.AcceptsViewings)
        {
            return Result.Failure<HousingViewingResponse>(HousingErrors.ViewingsNotAccepted);
        }

        var landlord = await _landlords.GetAsync(listing.LandlordId, cancellationToken);

        if (landlord is null || !landlord.CanLet)
        {
            return Result.Failure<HousingViewingResponse>(HousingErrors.LandlordSuspended);
        }

        if (landlord.OwnerUserId == caller.Value)
        {
            return Result.Failure<HousingViewingResponse>(HousingErrors.ViewingSelf);
        }

        var existing = await _viewings.GetOpenForSeekerAsync(
            listing.Id, caller.Value, cancellationToken);

        if (existing is not null)
        {
            return Result.Failure<HousingViewingResponse>(HousingErrors.ViewingAlreadyOpen);
        }

        // The address-harvesting bound. See the class summary.
        var open = await _viewings.CountOpenBySeekerAsync(caller.Value, cancellationToken);

        if (open >= _options.Value.MaxOpenViewingsPerSeeker)
        {
            return Result.Failure<HousingViewingResponse>(HousingErrors.TooManyViewings);
        }

        var now = _clock.UtcNow;

        var window = ViewingWindow.Create(
            command.Request.PreferredAt,
            confirmedAt: null,
            command.Request.DurationMinutes
                ?? _options.Value.DefaultViewingDurationMinutes,
            command.Request.IsVirtual);

        if (window.IsFailure)
        {
            return Result.Failure<HousingViewingResponse>(window.Error);
        }

        var viewing = HousingViewing.Request(
            HousingViewingId.From(_guids.NewSequentialGuid()),
            listing.Id,
            listing.LandlordId,
            caller.Value,
            window.Value,
            command.Request.Message,
            now,
            _guids.NewSequentialGuid());

        if (viewing.IsFailure)
        {
            return Result.Failure<HousingViewingResponse>(viewing.Error);
        }

        await _viewings.AddAsync(viewing.Value, cancellationToken);

        listing.RecordViewingRequest();
        landlord.RecordViewingRequest();

        var analytics = await _analytics.GetByListingAsync(listing.Id, cancellationToken);

        analytics?.RecordViewingRequest(now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        HousingTelemetry.ViewingsRequested.Add(
            1, new KeyValuePair<string, object?>("virtual", command.Request.IsVirtual));

        return Result.Success(
            HousingRelocationMapper.ToResponse(viewing.Value, listing, landlord));
    }
}
