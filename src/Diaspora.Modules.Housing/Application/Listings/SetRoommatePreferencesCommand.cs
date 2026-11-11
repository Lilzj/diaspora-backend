using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Records what an existing household is looking for.
/// <para>
/// Refused on anything that is not shared accommodation, and a gender preference is refused there
/// too unless the room really is in somebody home. That check is in the aggregate rather than in a
/// validator, because it is a rule about the property rather than about the request.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ListingEditOwn)]
[Auditable]
public sealed record SetRoommatePreferencesCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }

    public required SetRoommatePreferencesRequest Request { get; init; }
}
