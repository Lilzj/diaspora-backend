using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Marketplace.Domain.Events;

/// <summary>
/// Raised when a photograph, video or document is attached by reference.
/// <para>
/// <b>The handler for this is what claims the asset in Media, and claiming is not optional.</b>
/// Media sweeps assets nothing ever attached, so a listing whose photographs were uploaded and
/// never claimed loses them a day later — silently, because every upload succeeded and every
/// attach succeeded. On a marketplace that is the difference between a listing that sells and one
/// nobody opens.
/// </para>
/// </summary>
public sealed record ListingMediaAttachedDomainEvent : DomainEvent
{
    public required MarketplaceListingId ListingId { get; init; }

    public required SellerProfileId SellerId { get; init; }

    public required Guid MediaId { get; init; }

    public required ListingMediaRole Role { get; init; }
}
