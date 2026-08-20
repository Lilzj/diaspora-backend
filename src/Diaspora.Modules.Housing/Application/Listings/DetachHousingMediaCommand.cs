using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Removes a media reference from a property.
/// <para>
/// Never deletes the asset. Whether an unreferenced asset is swept is Media decision, and a
/// landlord who detaches a photograph may reattach it a minute later.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.ListingEditOwn)]
[Auditable]
public sealed record DetachHousingMediaCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }

    public required Guid MediaId { get; init; }
}
