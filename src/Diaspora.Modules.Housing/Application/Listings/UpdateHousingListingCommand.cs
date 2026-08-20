using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>Revises a property.</summary>
[Authorize(Permission = HousingPermissions.ListingEditOwn)]
[Auditable]
public sealed record UpdateHousingListingCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }

    public required UpdateHousingListingRequest Request { get; init; }
}
