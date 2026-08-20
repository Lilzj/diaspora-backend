using Diaspora.Application.Abstractions;
using Diaspora.Modules.Marketplace.Contracts.V1.Requests;

namespace Diaspora.Modules.Marketplace.Application.Listings;

/// <summary>
/// Asks Trust to vouch for a listing.
/// <para>
/// Returns nothing but success: Trust decides, not this module, and a command that returned a
/// verdict would be one whose contract implies it could.
/// </para>
/// </summary>
[Authorize(Permission = MarketplacePermissions.ListingSubmitVerification)]
[Auditable]
public sealed record SubmitListingVerificationCommand : ICommand
{
    public required Guid ListingId { get; init; }

    public SubmitVerificationRequest? Request { get; init; }
}
