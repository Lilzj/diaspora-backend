using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>Raised when a seller changes something buyers see about them.</summary>
public sealed record SellerUpdatedDomainEvent : DomainEvent
{
    public required SellerProfileId SellerId { get; init; }

    public UserId? ChangedBy { get; init; }

    public required IReadOnlyList<string> ChangedFields { get; init; }
}
