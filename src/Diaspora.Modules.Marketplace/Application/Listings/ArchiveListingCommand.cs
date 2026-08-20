using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Takes a listing down from every public surface, reversibly.
/// <para>
/// What a seller does when the item is gone, broken or simply not selling. Distinct from deletion,
/// which is final to every consumer — an archived listing can come back with its history intact.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.ListingArchive)]
[Auditable]
public sealed record ArchiveListingCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public ArchiveListingRequest? Request { get; init; }
}
