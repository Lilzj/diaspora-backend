using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Records a sale the seller made without a reservation behind it.
/// <para>
/// The path most peer-to-peer sales actually take: two people meet, money changes hands, and the
/// seller tells the platform afterwards. Everything on the request is optional, because forcing a
/// buyer identity or a price would push sellers into inventing one.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.ListingMarkSold)]
[Auditable]
public sealed record MarkListingSoldCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public MarkListingSoldRequest? Request { get; init; }
}
