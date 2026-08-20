using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Analytics;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Modules.Housing.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Housing.Application.Reservations;

/// <summary>
/// Takes a hold on a property.
/// <para>
/// <b>This is the handler the whole concurrency design exists for.</b> The units are taken through
/// <c>IHousingUnitReservationGuard</c> before the reservation is created, so a reservation never
/// exists without its hold. Two members reserving the last bed in the same instant both reach the
/// guard; exactly one commits, and the other is told the room has gone rather than being quietly
/// booked into a bed that is not there. The guard's implementation is the only thing that would
/// change if B10's recommended atomic counter primitive were ever built.
/// </para>
/// <para>
/// <b>The per-member ceiling is deliberately small.</b> Every open reservation is a home nobody
/// else can take. Somebody genuinely choosing between properties holds two or three; anybody
/// holding twelve is removing homes from the market for people who need them.
/// </para>
/// <para>
/// <b>The viewing requirement is the landlord's, and it is enforced rather than advisory.</b> A
/// landlord who asked for a visit first meant a visit — and on a platform where the alternative is
/// somebody committing to a home they have never seen, that is a protection worth enforcing.
/// </para>
/// </summary>
public sealed class CreateHousingReservationCommandHandler
    : ICommandHandler<CreateHousingReservationCommand, HousingReservationResponse>
{
    private readonly IHousingListingRepository _listings;
    private readonly IHousingReservationRepository _reservations;
    private readonly IHousingViewingRepository _viewings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly IHousingUnitReservationGuard _units;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IOptions<HousingOptions> _options;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public CreateHousingReservationCommandHandler(
        IHousingListingRepository listings,
        IHousingReservationRepository reservations,
        IHousingViewingRepository viewings,
        ILandlordProfileRepository landlords,
        IHousingAnalyticsRepository analytics,
        IHousingUnitReservationGuard units,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IOptions<HousingOptions> options,
        IGuidGenerator guids,
        IClock clock)
    {
        _listings = listings;
        _reservations = reservations;
        _viewings = viewings;
        _landlords = landlords;
        _analytics = analytics;
        _units = units;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _options = options;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<HousingReservationResponse>> Handle(
        CreateHousingReservationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<HousingReservationResponse>(caller.Error);
        }

        var listing = await _listings.GetAsync(
            HousingListingId.From(command.ListingId), cancellationToken);

        if (listing is null || !await _authorization.CanSeeAsync(listing, cancellationToken))
        {
            return Result.Failure<HousingReservationResponse>(HousingErrors.ListingNotFound);
        }

        var now = _clock.UtcNow;

        var eligibility = await CheckEligibilityAsync(
            listing, caller.Value, now, cancellationToken);

        if (eligibility.IsFailure)
        {
            return Result.Failure<HousingReservationResponse>(eligibility.Error);
        }

        var terms = BuildTerms(command, listing, now);

        if (terms.IsFailure)
        {
            return Result.Failure<HousingReservationResponse>(terms.Error);
        }

        var units = Math.Max(1, command.Request.Units);

        var stayCheck = CheckStay(listing, terms.Value, units);

        if (stayCheck.IsFailure)
        {
            return Result.Failure<HousingReservationResponse>(stayCheck.Error);
        }

        // The units first, through the guard. See the class summary: a reservation opened before
        // its hold is a window in which a second member takes the same room.
        var held = await _units.TryHoldAsync(listing.Id, units, now, cancellationToken);

        if (held.IsFailure)
        {
            HousingTelemetry.ReservationRacesLost.Add(1);

            return Result.Failure<HousingReservationResponse>(held.Error);
        }

        var reservation = HousingReservation.Open(
            HousingReservationId.From(_guids.NewSequentialGuid()),
            listing.Id,
            listing.LandlordId,
            caller.Value,
            units,
            terms.Value,
            now,
            _guids.NewSequentialGuid(),
            eligibility.Value,
            command.Request.Message);

        if (reservation.IsFailure)
        {
            return Result.Failure<HousingReservationResponse>(reservation.Error);
        }

        await _reservations.AddAsync(reservation.Value, cancellationToken);

        listing.RecordReservationRequest();

        var analytics = await _analytics.GetByListingAsync(listing.Id, cancellationToken);

        analytics?.RecordReservation(now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        HousingTelemetry.ReservationsCreated.Add(1);

        var landlord = await _landlords.GetForReadAsync(listing.LandlordId, cancellationToken);

        return Result.Success(
            HousingRelocationMapper.ToResponse(reservation.Value, listing, landlord));
    }

    /// <summary>
    /// Everything that must be true about the member and the property before units are touched.
    /// </summary>
    /// <returns>The viewing this follows from, where the landlord required one.</returns>
    private async Task<Result<HousingViewingId?>> CheckEligibilityAsync(
        HousingListing listing,
        SharedKernel.Identity.UserId callerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!listing.AcceptsReservations(now))
        {
            return Result.Failure<HousingViewingId?>(HousingErrors.ReservationsNotAccepted);
        }

        var landlord = await _landlords.GetAsync(listing.LandlordId, cancellationToken);

        if (landlord is null || !landlord.CanLet)
        {
            return Result.Failure<HousingViewingId?>(HousingErrors.LandlordSuspended);
        }

        if (landlord.OwnerUserId == callerId)
        {
            return Result.Failure<HousingViewingId?>(HousingErrors.ReservationSelf);
        }

        var existing = await _reservations.GetActiveForSeekerAsync(
            listing.Id, callerId, cancellationToken);

        if (existing is not null)
        {
            return Result.Failure<HousingViewingId?>(HousingErrors.ReservationAlreadyOpen);
        }

        var active = await _reservations.CountActiveBySeekerAsync(callerId, cancellationToken);

        if (active >= _options.Value.MaxActiveReservationsPerSeeker)
        {
            return Result.Failure<HousingViewingId?>(HousingErrors.TooManyReservations);
        }

        if (!listing.RequiresViewingBeforeReservation)
        {
            return Result.Success<HousingViewingId?>(null);
        }

        // The landlord asked for a visit first, and meant it.
        if (!await _viewings.HasCompletedViewingAsync(listing.Id, callerId, cancellationToken))
        {
            return Result.Failure<HousingViewingId?>(
                HousingErrors.ReservationRequiresViewing);
        }

        return Result.Success<HousingViewingId?>(null);
    }

    /// <summary>
    /// Builds the recorded terms, defaulting the hold expiry when the parties agreed none.
    /// <para>
    /// A hold with no end is a home removed from the market by somebody who stopped replying, so
    /// there is always one.
    /// </para>
    /// </summary>
    private Result<ReservationTerms> BuildTerms(
        CreateHousingReservationCommand command,
        HousingListing listing,
        DateTimeOffset now)
    {
        var request = command.Request;

        var period = Enum.TryParse<RentPeriod>(
            request.RentPeriod, ignoreCase: true, out var parsedPeriod)
            ? parsedPeriod
            : listing.Price.Period;

        var leaseType = Enum.TryParse<LeaseType>(
            request.LeaseType, ignoreCase: true, out var parsedLease)
            ? parsedLease
            : listing.LeaseType;

        return ReservationTerms.Create(
            request.MoveInDate,
            request.MoveOutDate,
            now.AddHours(Math.Max(1, _options.Value.DefaultReservationHoldHours)),
            request.AgreedRent ?? listing.Price.Amount,
            request.AgreedDeposit ?? listing.Price.DepositAmount,
            request.CurrencyCode ?? listing.Price.CurrencyCode,
            period,
            leaseType,
            Math.Max(1, request.Occupants));
    }

    /// <summary>
    /// Checks the intended stay and the occupant count against what the property offers.
    /// <para>
    /// Both refuse rather than clamp: a member booking six nights at a place with a seven-night
    /// minimum needs to know before they arrange anything else, not to discover a silently
    /// lengthened booking.
    /// </para>
    /// </summary>
    private static Result CheckStay(
        HousingListing listing, ReservationTerms terms, int units)
    {
        if (terms.StayNights is { } nights && !listing.Availability.AcceptsStay(nights))
        {
            return Result.Failure(HousingErrors.StayLengthInvalid);
        }

        if (listing.Capacity.MaximumOccupants is { } maximum
            && terms.Occupants > maximum * units)
        {
            return Result.Failure(HousingErrors.OccupancyInvalid);
        }

        return Result.Success();
    }
}
