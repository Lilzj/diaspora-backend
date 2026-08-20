using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a Trust outcome lands on a seller.
/// <para>
/// <b>This is the event the brief's "Seller Verified" contract is published from</b>, and it is
/// the one every seller badge in the module is downstream of. It is also the one place a
/// revocation propagates: a seller whose verification is withdrawn stops being a "Verified
/// Business" on every card in the same beat, because the type is computed rather than stored.
/// </para>
/// </summary>
public sealed record SellerVerificationChangedDomainEvent : DomainEvent
{
    public required SellerProfileId SellerId { get; init; }

    public required ListingVerificationState PreviousState { get; init; }

    public required ListingVerificationState CurrentState { get; init; }

    public Guid? VerificationId { get; init; }

    public string? Badge { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
