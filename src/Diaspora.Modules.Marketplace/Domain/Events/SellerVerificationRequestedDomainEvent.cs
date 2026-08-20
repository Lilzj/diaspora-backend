using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>Raised when a seller asks Trust to verify who they are. Says nothing about the outcome.</summary>
public sealed record SellerVerificationRequestedDomainEvent : DomainEvent
{
    public required SellerProfileId SellerId { get; init; }

    public required UserId RequestedBy { get; init; }

    public Guid? VerificationId { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }
}
