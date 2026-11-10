using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Marketplace.Application.Promotions;

/// <summary>Ends a promotion before its window closes.</summary>
[RequirePermission(MarketplacePermissions.ManagePromotions)]
[Auditable]
public sealed record CancelPromotionCommand : ICommand
{
    public required Guid PromotionId { get; init; }
}
