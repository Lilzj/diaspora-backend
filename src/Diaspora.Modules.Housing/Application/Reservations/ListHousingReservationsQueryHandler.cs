using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Reservations;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Reservations;

/// <summary>
/// The caller's reservations, as a member or as a landlord.
/// <para>
/// Always scoped to the caller, and staff get no bypass: a list of who is about to move where is
/// not something a role check in a general-purpose helper should hand out.
/// </para>
/// </summary>
public sealed class ListHousingReservationsQueryHandler
    : IQueryHandler<ListHousingReservationsQuery, IReadOnlyList<HousingReservationResponse>>
{
    private readonly IHousingReservationRepository _reservations;
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly HousingAuthorizationService _authorization;

    public ListHousingReservationsQueryHandler(
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

    public async Task<Result<IReadOnlyList<HousingReservationResponse>>> Handle(
        ListHousingReservationsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<HousingReservationResponse>>(caller.Error);
        }

        var asLandlord = string.Equals(
            request.Side, "landlord", StringComparison.OrdinalIgnoreCase);

        var landlord = asLandlord
            ? await _authorization.FindCallerLandlordAsync(cancellationToken)
            : null;

        if (asLandlord && landlord is null)
        {
            return Result.Success<IReadOnlyList<HousingReservationResponse>>([]);
        }

        var filter = new ReservationQueryFilter
        {
            SeekerUserId = asLandlord ? null : caller.Value,
            LandlordId = landlord?.Id,
            HoldingUnitsOnly = request.ActiveOnly,
            Limit = request.Limit ?? Domain.Policies.HousingLimits.DefaultPageSize
        };

        var page = await _reservations.QueryAsync(
            filter, request.Cursor, filter.EffectiveLimit, cancellationToken);

        return Result.Success(await ProjectAsync(page.Items, cancellationToken));
    }

    /// <summary>Resolves the listing titles and landlord names a page needs, batched.</summary>
    private async Task<IReadOnlyList<HousingReservationResponse>> ProjectAsync(
        IReadOnlyList<HousingReservation> reservations, CancellationToken cancellationToken)
    {
        if (reservations.Count == 0)
        {
            return [];
        }

        var listings = (await _listings.GetManyAsync(
                [.. reservations.Select(reservation => reservation.ListingId).Distinct()],
                cancellationToken))
            .ToDictionary(listing => listing.Id);

        var landlords = await _landlords.GetManyAsync(
            [.. reservations.Select(reservation => reservation.LandlordId).Distinct()],
            cancellationToken);

        return
        [
            .. reservations.Select(reservation => HousingRelocationMapper.ToResponse(
                reservation,
                listings.GetValueOrDefault(reservation.ListingId),
                landlords.GetValueOrDefault(reservation.LandlordId)))
        ];
    }
}
