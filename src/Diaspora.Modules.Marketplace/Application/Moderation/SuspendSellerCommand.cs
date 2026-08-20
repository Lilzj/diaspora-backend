using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>
/// Bars a seller from trading, or lets them back.
/// <para>
/// The brief's seller-suspension hook. Their listings go dark through the status event; their own
/// state is untouched, so reinstating restores exactly what was there.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.Moderate)]
[Auditable]
public sealed record SuspendSellerCommand : ICommand
{
    public required Guid SellerId { get; init; }

    public required SuspendSellerRequest Request { get; init; }
}
