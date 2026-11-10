using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;
using Diaspora.Modules.Housing.Contracts.V1.Responses;

namespace Diaspora.Modules.Housing.Application.Landlords;

/// <summary>Revises the caller landlord profile.</summary>
[RequirePermission(HousingPermissions.LandlordManage)]
[Auditable]
public sealed record UpdateLandlordCommand : ICommand<LandlordProfileResponse>
{
    public required UpdateLandlordRequest Request { get; init; }
}
