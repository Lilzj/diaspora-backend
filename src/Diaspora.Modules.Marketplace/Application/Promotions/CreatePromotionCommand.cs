using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Promotions;

/// <summary>
/// Grants a listing extra placement for a window.
/// <para>
/// Staff-only today. Nothing here charges anybody — payments are out of scope for B10, and the
/// external reference is where a future billing module's receipt lands without a schema change.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.ManagePromotions)]
[Auditable]
public sealed record CreatePromotionCommand : ICommand<ListingPromotionResponse>
{
    public required Guid ListingId { get; init; }

    public required CreatePromotionRequest Request { get; init; }
}
