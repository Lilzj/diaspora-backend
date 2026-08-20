using Diaspora.Application.Abstractions;
using Diaspora.Modules.Housing.Contracts.V1.Requests;

namespace Diaspora.Modules.Housing.Application.Listings;

/// <summary>Asks Trust to vouch for a particular property. Returns a case identifier.</summary>
[Authorize(Permission = HousingPermissions.ListingSubmitVerification)]
[Auditable]
public sealed record SubmitListingVerificationCommand : ICommand<Guid>
{
    public required Guid ListingId { get; init; }

    public required SubmitHousingVerificationRequest Request { get; init; }
}
