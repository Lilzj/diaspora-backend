using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a soft-deleted or archived listing comes back.
/// <para>
/// The counterpart to <see cref="ListingDeletedDomainEvent"/>, and it exists for the same reason:
/// something has to tell the index the listing is real again. Without it a restored listing is one
/// the seller can see on their own dashboard and no buyer can ever find.
/// </para>
/// </summary>
public sealed record ListingRestoredDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    /// <summary>True when it came back from soft deletion, false when from the archive.</summary>
    public required bool WasDeleted { get; init; }
}
