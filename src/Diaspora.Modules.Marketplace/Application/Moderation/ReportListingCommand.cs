using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Moderation;

/// <summary>
/// Reports a listing.
/// <para>
/// The permission sits at the low floor and survives a restriction, because a restriction that
/// silenced the person best placed to flag a scam would be the platform working against itself.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.Report)]
[Auditable]
public sealed record ReportListingCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public required ReportListingRequest Request { get; init; }
}
