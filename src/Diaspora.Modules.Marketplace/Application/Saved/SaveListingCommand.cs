using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Saved;

/// <summary>
/// Adds a listing to the caller's watchlist, or updates the note on one already there.
/// <para>
/// Idempotent by design and by database constraint: a double-tap on a slow connection updates the
/// same row rather than creating a second and inflating the listing's save count forever.
/// </para>
/// </summary>
[RequirePermission(MarketplacePermissions.SavedManage)]
public sealed record SaveListingCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public SaveListingRequest? Request { get; init; }
}
