using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>Attaches a Media asset to a property, by reference.</summary>
[RequirePermission(HousingPermissions.ListingEditOwn)]
[Auditable]
public sealed record AttachHousingMediaCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }

    public required AttachHousingMediaRequest Request { get; init; }
}
