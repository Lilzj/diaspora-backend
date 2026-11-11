using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Sellers;

/// <summary>Asks Trust to verify the caller's seller profile.</summary>
[RequirePermission(MarketplacePermissions.SellerSubmitVerification)]
[Auditable]
public sealed record SubmitSellerVerificationCommand : ICommand
{
    public SubmitVerificationRequest? Request { get; init; }
}
