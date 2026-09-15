using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Application.Mapping;
using Diaspora.Modules.Housing.Application.Services;
using Diaspora.Modules.Housing.Contracts.V1.Responses;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Landlords;
using Diaspora.Modules.Housing.Domain.Listings;
using Diaspora.Modules.Housing.Domain.Viewings;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Application.Viewings;

/// <summary>
/// Reads one viewing, with its history.
/// <para>
/// The history is the point of the read. "You never confirmed a time" and "I cancelled two days
/// before" are both claims two people make in good faith about the same viewing, and an append-only
/// trail neither of them can edit is the only thing that settles it.
/// </para>
/// </summary>
public sealed class GetViewingQueryHandler
    : IQueryHandler<GetViewingQuery, HousingViewingResponse>
{
    private readonly IHousingViewingRepository _viewings;
    private readonly IHousingListingRepository _listings;
    private readonly ILandlordProfileRepository _landlords;
    private readonly HousingAuthorizationService _authorization;
    private readonly IMemberProfileService _members;

    public GetViewingQueryHandler(
        IHousingViewingRepository viewings,
        IHousingListingRepository listings,
        ILandlordProfileRepository landlords,
        HousingAuthorizationService authorization,
        IMemberProfileService members)
    {
        _viewings = viewings;
        _listings = listings;
        _landlords = landlords;
        _authorization = authorization;
        _members = members;
    }

    public async Task<Result<HousingViewingResponse>> Handle(
        GetViewingQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var viewing = await _viewings.GetForReadAsync(
            HousingViewingId.From(request.ViewingId), cancellationToken);

        if (viewing is null)
        {
            return Result.Failure<HousingViewingResponse>(HousingErrors.ViewingNotFound);
        }

        var party = await _authorization.RequirePartyAsync(
            viewing.SeekerUserId,
            viewing.LandlordId,
            HousingErrors.NotViewingParty,
            cancellationToken);

        if (party.IsFailure)
        {
            return Result.Failure<HousingViewingResponse>(party.Error);
        }

        var listing = await _listings.GetForReadAsync(viewing.ListingId, cancellationToken);
        var landlord = await _landlords.GetForReadAsync(viewing.LandlordId, cancellationToken);
        var seeker = await ViewingSeekers.ResolveAsync(_members, viewing, cancellationToken);

        return Result.Success(
            HousingRelocationMapper.ToResponse(viewing, listing, landlord, seeker));
    }
}
