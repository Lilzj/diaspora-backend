using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a listing is soft-deleted.
/// <para>
/// Raised explicitly by the aggregate rather than left to the change tracker, for the reason
/// B1.0.1 and B6 both record: a delete that happens inside the tracker cannot also raise an event,
/// and downstream de-indexing needs one. A listing that stays in a search index after deletion is
/// a listing buyers keep enquiring about.
/// </para>
/// </summary>
public sealed record ListingDeletedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}
