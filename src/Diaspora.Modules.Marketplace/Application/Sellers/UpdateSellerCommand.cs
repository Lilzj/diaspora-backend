using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Sellers;

/// <summary>Updates the caller's own seller profile.</summary>
[RequirePermission(MarketplacePermissions.SellerManage)]
[Auditable]
public sealed record UpdateSellerCommand : ICommand<SellerProfileResponse>
{
    public required UpdateSellerRequest Request { get; init; }
}
