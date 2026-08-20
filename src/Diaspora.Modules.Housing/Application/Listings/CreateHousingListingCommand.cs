using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>
/// Drafts a property.
/// <para>
/// Transactional: the listing, its analytics record and the neighbourhood profile it may create all
/// commit together, and a listing whose neighbourhood profile failed to save would point at nothing.
/// </para>
/// </summary>
[Authorize(Permission = HousingPermissions.ListingCreate)]
[Transactional(Reason = "Writes the listing, its analytics record and possibly a new neighbourhood profile.")]
[Auditable]
public sealed record CreateHousingListingCommand : ICommand<HousingListingResponse>
{
    public required CreateHousingListingRequest Request { get; init; }
}
