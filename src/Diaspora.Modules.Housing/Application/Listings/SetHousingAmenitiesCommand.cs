using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>Replaces a property amenity list, checked against the catalogue.</summary>
[RequirePermission(HousingPermissions.ListingEditOwn)]
[Auditable]
public sealed record SetHousingAmenitiesCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }

    public required SetHousingAmenitiesRequest Request { get; init; }
}
