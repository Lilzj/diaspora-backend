using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Landlords;

/// <summary>Sets up the caller landlord profile.</summary>
[RequirePermission(HousingPermissions.LandlordCreate)]
[Auditable]
public sealed record RegisterLandlordCommand : ICommand<LandlordProfileResponse>
{
    public required RegisterLandlordRequest Request { get; init; }
}
