using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>
/// A moderator's decision about a listing.
/// <para>
/// Every action is reversible and every one demands a reason. The reason is not bureaucracy: it is
/// what a second moderator reads before overturning the first, and what an appeal is answered from.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.Moderate)]
[Auditable]
public sealed record ModerateListingCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public required ModerateListingRequest Request { get; init; }
}
