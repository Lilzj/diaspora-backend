using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a seller is suspended, reinstated or closes.
/// <para>
/// <b>The consumer that matters is the one that takes the seller's listings dark.</b> A suspension
/// that left a hundred listings live would be a suspension in name only, and doing the cascade
/// inside the seller aggregate would mean loading every listing the seller owns to change one
/// field on the profile.
/// </para>
/// </summary>
public sealed record SellerStatusChangedDomainEvent : DomainEvent
{
    public required SellerProfileId SellerId { get; init; }

    public required SellerStatus PreviousStatus { get; init; }

    public required SellerStatus CurrentStatus { get; init; }

    public UserId? ChangedBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
