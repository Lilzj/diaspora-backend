using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>Replaces a listing's structured attributes wholesale.</summary>
[RequirePermission(MarketplacePermissions.ListingEditOwn)]
[Auditable]
public sealed record SetListingAttributesCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public required SetListingAttributesRequest Request { get; init; }
}
