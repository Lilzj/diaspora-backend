using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Restates when a property is available and how many units it has.
/// <para>
/// The unit count is refused below what is already reserved or occupied: a hostel cannot shrink to
/// four beds while five people are booked into them.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.ListingEditOwn)]
[Auditable]
public sealed record SetHousingAvailabilityCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }

    public required SetAvailabilityRequest Request { get; init; }
}
