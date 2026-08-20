using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;
using Diaspora.Modules.Marketplace.Contracts.V1.Responses;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Updates everything a buyer reads that is not a list.
/// <para>
/// Not <c>[Transactional]</c>: it writes one aggregate, and the shared pipeline saves it. Declaring
/// a transaction for a single-aggregate write would be a promise the module does not need to make.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.ListingEditOwn)]
[Auditable]
public sealed record UpdateListingCommand : ICommand<ListingResponse>
{
    public required Guid ListingId { get; init; }

    public required UpdateListingRequest Request { get; init; }
}
