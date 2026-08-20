using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>Raised when a promotion is cancelled or its window closes.</summary>
public sealed record ListingPromotionEndedDomainEvent : DomainEvent
{
    public required ListingPromotionId PromotionId { get; init; }

    public required MarketplaceListingId ListingId { get; init; }

    public required PromotionKind Kind { get; init; }

    /// <summary>True when a person ended it early, false when the clock did.</summary>
    public required bool WasCancelled { get; init; }

    public UserId? EndedBy { get; init; }

    public required DateTimeOffset EndedAt { get; init; }
}
