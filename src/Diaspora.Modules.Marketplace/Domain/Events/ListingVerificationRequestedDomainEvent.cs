using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a seller asks Trust to vouch for a listing. Says nothing about the outcome.
/// <para>
/// <see cref="VerificationId"/> is Trust's case identifier, and stamping it is what lets an
/// outcome find this listing again: Trust's events name a subject, and a member may have a hundred
/// listings, so the correlation has to be the case.
/// </para>
/// </summary>
public sealed record ListingVerificationRequestedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required UserId RequestedBy { get; init; }

    public Guid? VerificationId { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }
}
