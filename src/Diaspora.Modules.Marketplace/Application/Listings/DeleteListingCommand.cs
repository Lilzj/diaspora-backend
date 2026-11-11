using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Deletes a listing. Soft, always — the row survives for audit and for undelete.
/// <para>
/// Final to every consumer, unlike archiving: the reference is dropped, the index entry removed and
/// the card closed. The difference is what a buyer holding a saved listing sees.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.ListingDeleteOwn)]
[Auditable]
public sealed record DeleteListingCommand : ICommand
{
    public required Guid ListingId { get; init; }
}
