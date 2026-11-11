using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>Replaces a property house rules.</summary>
[RequirePermission(HousingPermissions.ListingEditOwn)]
[Auditable]
public sealed record SetHouseRulesCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }

    public required SetHouseRulesRequest Request { get; init; }
}
