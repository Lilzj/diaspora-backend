using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Reservations;

/// <summary>
/// Reads one reservation, with its history.
/// <para>
/// The history is what a disagreement is settled from. A reservation is the point at which a member
/// commits to a home and, in the real world, starts moving money around outside the platform — and
/// when something goes wrong, the sequence of who agreed what and when is the whole of what Housing
/// can offer either party.
/// </para>
/// </summary>
public sealed class GetHousingReservationQueryHandler
    : IQueryHandler<GetHousingReservationQuery, HousingReservationResponse>
{
    private readonly IHousingReservationRepository _reservations;
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly HousingAuthorizationService _authorization;

    public GetHousingReservationQueryHandler(
        IHousingReservationRepository reservations,
        IHousingListingRepository listings,
        ILandlordProfileRepository landlords,
        HousingAuthorizationService authorization)
    {
        _reservations = reservations;
        _listings = listings;
        _landlords = landlords;
        _authorization = authorization;
    }

    public async Task<Result<HousingReservationResponse>> Handle(
        GetHousingReservationQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reservation = await _reservations.GetForReadAsync(
            HousingReservationId.From(request.ReservationId), cancellationToken);

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

        var listing = await _listings.GetForReadAsync(reservation.ListingId, cancellationToken);
        var landlord = await _landlords.GetForReadAsync(
            reservation.LandlordId, cancellationToken);

        return Result.Success(
            HousingRelocationMapper.ToResponse(reservation, listing, landlord));
    }
}
