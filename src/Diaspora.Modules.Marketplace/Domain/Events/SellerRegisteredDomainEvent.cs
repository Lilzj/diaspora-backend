using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>Raised when a member opens a seller profile.</summary>
public sealed record SellerRegisteredDomainEvent : DomainEvent
{
    public required SellerProfileId SellerId { get; init; }

    public required UserId OwnerId { get; init; }

    public required string DisplayName { get; init; }

    public required string Slug { get; init; }

    public required SellerKind Kind { get; init; }

    public required string CountryCode { get; init; }

    /// <summary>The Providers shop behind the storefront, when there is one.</summary>
    public Guid? ProviderId { get; init; }
}
