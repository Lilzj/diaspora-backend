using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Brings an archived or deleted property back, as a draft.
/// <para>
/// Never straight to available: a property that has been off the market wants its rent, its dates
/// and its photographs looked at before anybody is shown it again.
/// </para>
/// </summary>
[RequirePermission(HousingPermissions.ListingRestore)]
[Auditable]
public sealed record RestoreHousingListingCommand : ICommand<HousingListingResponse>
{
    public required Guid ListingId { get; init; }
}
