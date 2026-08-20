using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when moderation changes what the public may see.
/// <para>
/// The index consumer keys on this as hard as it does on publication: a listing hidden for
/// suspected fraud that stays in the search index is the platform continuing to advertise a scam
/// after deciding it was one.
/// </para>
/// </summary>
public sealed record ListingModerationChangedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required ListingModerationState PreviousState { get; init; }

    public required ListingModerationState CurrentState { get; init; }

    public UserId? ModeratorId { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
