using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Marketplace.Application.Saved;

/// <summary>
/// Removes a listing from the caller's watchlist.
/// <para>
/// Hard-deleted, not soft. Unsaving is a member asking the platform to stop showing them something,
/// and a tombstone that kept the row would be retaining exactly the browsing history they asked it
/// to forget.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.SavedManage)]
public sealed record RemoveSavedListingCommand : ICommand
{
    public required Guid ListingId { get; init; }
}
