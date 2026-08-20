using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Brings a listing back — from the archive, or from a soft deletion.
/// <para>
/// It returns as a draft rather than straight to live, deliberately: a listing that has been off
/// the market wants its price and its stock looked at before anybody can buy from it again.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.ListingRestore)]
[Auditable]
public sealed record RestoreListingCommand : ICommand
{
    public required Guid ListingId { get; init; }
}
