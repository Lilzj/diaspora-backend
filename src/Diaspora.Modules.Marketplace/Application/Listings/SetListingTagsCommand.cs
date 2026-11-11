using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>Replaces a listing's tags wholesale — that is how the chip editor saves.</summary>
[RequirePermission(MarketplacePermissions.ListingEditOwn)]
[Auditable]
public sealed record SetListingTagsCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public required SetListingTagsRequest Request { get; init; }
}
