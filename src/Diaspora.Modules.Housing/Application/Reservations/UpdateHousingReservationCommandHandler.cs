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
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.Modules.Housing.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Reservations;

/// <summary>
/// Moves a reservation along, and settles the units in the same unit of work.
/// <para>
/// <b>The settlement is the point of this handler.</b> Every exit moves units between exactly two
/// of held, occupied and free, and it happens here — once — rather than at each transition's call
/// site. A status change that committed without its settlement would leave a home nobody can ever
/// let, which is the failure this module works hardest to make impossible.
/// </para>
/// <para>
/// <b>Who may do what differs by action.</b> Only the landlord may accept or decline — it is their
/// property. Either party may cancel, always, because a hold that could not be released traps a
/// home. Occupation may be recorded by either, because both know when somebody moved in.
/// </para>
/// </summary>
public sealed class UpdateHousingReservationCommandHandler
    : ICommandHandler<UpdateHousingReservationCommand, HousingReservationResponse>
{
    private readonly IHousingReservationRepository _reservations;
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly IHousingAnalyticsRepository _analytics;
    private readonly IHousingUnitReservationGuard _units;
    private readonly IUnitOfWork _unitOfWork;
    private readonly HousingAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public UpdateHousingReservationCommandHandler(
        IHousingReservationRepository reservations,
        IHousingListingRepository listings,
        ILandlordProfileRepository landlords,
        IHousingAnalyticsRepository analytics,
        IHousingUnitReservationGuard units,
        IUnitOfWork unitOfWork,
        HousingAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock)
    {
        _reservations = reservations;
        _listings = listings;
        _landlords = landlords;
        _analytics = analytics;
        _units = units;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<HousingReservationResponse>> Handle(
        UpdateHousingReservationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reservation = await _reservations.GetAsync(
            HousingReservationId.From(command.ReservationId), cancellationToken);

        if (reservation is null)
        {
            return Result.Failure<HousingReservationResponse>(
                HousingErrors.ReservationNotFound);
        }

        var party = await _authorization.RequirePartyAsync(
            reservation.SeekerUserId,
            reservation.LandlordId,
            HousingErrors.NotReservationParty,
            cancellationToken);

        if (party.IsFailure)
        {
            return Result.Failure<HousingReservationResponse>(party.Error);
        }

        var landlord = await _landlords.GetAsync(reservation.LandlordId, cancellationToken);
        var isLandlord = landlord?.OwnerUserId == party.Value;
        var now = _clock.UtcNow;
        var previous = reservation.Status;

        var applied = Apply(command, reservation, party.Value, isLandlord, now);

        if (applied.IsFailure)
        {
            return Result.Failure<HousingReservationResponse>(applied.Error);
        }

        var settled = await SettleAsync(reservation, previous, now, cancellationToken);

        if (settled.IsFailure)
        {
            return Result.Failure<HousingReservationResponse>(settled.Error);
        }

        await RecordOutcomeAsync(reservation, previous, landlord, now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var listing = await _listings.GetForReadAsync(reservation.ListingId, cancellationToken);

        return Result.Success(
            HousingRelocationMapper.ToResponse(reservation, listing, landlord));
    }

    private Result Apply(
        UpdateHousingReservationCommand command,
        HousingReservation reservation,
        UserId actorId,
        bool isLandlord,
        DateTimeOffset now)
    {
        var entryId = _guids.NewSequentialGuid();
        var request = command.Request;

        return request.Action?.Trim().ToLowerInvariant() switch
        {
            "accept" => isLandlord
                ? reservation.Accept(actorId, request.HoldExpiresAt, now, entryId)
                : Result.Failure(HousingErrors.NotReservationParty),

            "decline" => isLandlord
                ? reservation.Decline(actorId, request.Reason, now, entryId)
                : Result.Failure(HousingErrors.NotReservationParty),

            "leasesigned" or "lease-signed" =>
                reservation.RecordLeaseSigned(actorId, now, entryId),

            "occupy" => reservation.RecordOccupied(actorId, now, entryId),

            "cancel" => reservation.Cancel(actorId, request.Reason, now, entryId),

            "extend" => request.HoldExpiresAt is { } expiry
                ? reservation.ExtendHold(expiry, now)
                : Result.Failure(HousingErrors.ReservationWindowInvalid),

            _ => Result.Failure(HousingErrors.ReservationTransitionInvalid)
        };
    }

    /// <summary>
    /// Moves the units to match the new state.
    /// <para>
    /// Driven by the transition rather than by the requested action, so a move the aggregate
    /// refused or reinterpreted cannot settle units for something that did not happen. An extension
    /// changes no state and settles nothing.
    /// </para>
    /// </summary>
    private async Task<Result> SettleAsync(
        HousingReservation reservation,
        HousingReservationStatus previous,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (reservation.Status == previous)
        {
            return Result.Success();
        }

        if (reservation.Status == HousingReservationStatus.Occupied)
        {
            var occupied = await _units.OccupyAsync(
                reservation.ListingId, reservation.Units, now, cancellationToken);

            return occupied.IsFailure ? Result.Failure(occupied.Error) : Result.Success();
        }

        if (!HousingReservationStatusTransitions.IsAbandoned(reservation.Status))
        {
            return Result.Success();
        }

        var released = await _units.ReleaseAsync(
            reservation.ListingId, reservation.Units, now, cancellationToken);

        return released.IsFailure ? Result.Failure(released.Error) : Result.Success();
    }

    /// <summary>Records the outcome on the listing, the landlord and the measured record.</summary>
    private async Task RecordOutcomeAsync(
        HousingReservation reservation,
        HousingReservationStatus previous,
        LandlordProfile? landlord,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (reservation.Status == previous)
        {
            return;
        }

        var analytics = await _analytics.GetByListingAsync(
            reservation.ListingId, cancellationToken);

        if (reservation.Status == HousingReservationStatus.Occupied)
        {
            var listing = await _listings.GetAsync(reservation.ListingId, cancellationToken);

            listing?.RecordOccupation(
                reservation.Units, reservation.SeekerUserId, reservation.Id, now);

            landlord?.RecordLet();

            analytics?.RecordOccupation(
                now,
                reservation.Terms.AgreedRent,
                reservation.Terms.CurrencyCode,
                reservation.Terms.Period);

            if (listing?.FirstPublishedAt is { } published)
            {
                HousingTelemetry.TimeToLetDays.Record((now - published).TotalDays);
            }

            HousingTelemetry.Occupations.Add(1);

            return;
        }

        if (!HousingReservationStatusTransitions.IsAbandoned(reservation.Status))
        {
            return;
        }

        analytics?.RecordReservationAbandoned(now);

        HousingTelemetry.ReservationsAbandoned.Add(
            1,
            new KeyValuePair<string, object?>("outcome", reservation.Status.ToString()));
    }
}
