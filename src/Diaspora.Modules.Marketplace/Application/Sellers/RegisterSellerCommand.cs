using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Sellers;

/// <summary>Opens a seller profile. One per member, enforced by a unique index.</summary>
[Authorize(Permission = MarketplacePermissions.SellerCreate)]
[Auditable]
public sealed record RegisterSellerCommand : ICommand<SellerProfileResponse>
{
    public required RegisterSellerRequest Request { get; init; }
}
