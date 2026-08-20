using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Restates how many units a listing has.
/// <para>
/// Its own command, because stock is the one thing a shop changes without touching anything else —
/// and a restock that failed the revision check because somebody fixed a typo would be infuriating.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.ListingEditOwn)]
[Auditable]
public sealed record SetListingStockCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public required SetListingStockRequest Request { get; init; }
}
