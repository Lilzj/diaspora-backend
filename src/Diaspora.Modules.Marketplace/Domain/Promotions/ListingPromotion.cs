using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Promotions;

/// <summary>
/// A window during which a listing gets extra placement.
/// <para>
/// <b>Its own aggregate rather than a flag on the listing, because a promotion has a life.</b> It
/// starts, it ends, somebody granted it, and it will one day have been paid for. A boolean on the
/// listing cannot expire itself, cannot say who granted it, and cannot be audited — and the flag
/// that is left set after the window closes is the marketplace giving away placement it meant to
/// sell.
/// </para>
/// <para>
/// <b>Nothing here charges anybody.</b> Payments are explicitly out of scope for B10. A promotion
/// is granted by staff today and by a billing module later; this records that it was, and
/// <see cref="ExternalReference"/> is where that module's receipt identifier will land without a
/// schema change.
/// </para>
/// <para>
/// <b>Placement, never rank suppression.</b> A promotion lifts one listing; it never pushes
/// another down. On a marketplace, a paid slot that could bury a better-matched free listing is
/// the point at which search results stop being an answer to the buyer's question.
/// </para>
/// </summary>
public sealed class ListingPromotion : AggregateRoot<ListingPromotionId>, IAuditableEntity
{
    private ListingPromotion(
        ListingPromotionId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        PromotionKind kind,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        UserId? grantedBy,
        DateTimeOffset createdAt)
        : base(id)
    {
        ListingId = listingId;
        SellerId = sellerId;
        Kind = kind;
        StartsAt = startsAt;
        EndsAt = endsAt;
        GrantedBy = grantedBy;
        CreatedAt = createdAt;
    }

    private ListingPromotion() { }

    public MarketplaceListingId ListingId { get; private set; } = default!;

    public SellerProfileId SellerId { get; private set; } = default!;

    public PromotionKind Kind { get; private set; }

    public DateTimeOffset StartsAt { get; private set; }

    public DateTimeOffset EndsAt { get; private set; }

    /// <summary>Who granted it. Null when a scheduled job or a future billing module did.</summary>
    public UserId? GrantedBy { get; private set; }

    /// <summary>Whether a person ended it before its window closed.</summary>
    public bool IsCancelled { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public UserId? CancelledBy { get; private set; }

    /// <summary>
    /// A receipt or order identifier from whatever granted this. Unused today, and the seam a
    /// billing module lands on without a migration.
    /// </summary>
    public string? ExternalReference { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    /// <summary>Whether the promotion is running right now.</summary>
    public bool IsActive(DateTimeOffset now) =>
        !IsCancelled && StartsAt <= now && EndsAt > now;

    /// <summary>Whether its window has closed, one way or another.</summary>
    public bool HasEnded(DateTimeOffset now) => IsCancelled || EndsAt <= now;

    /// <summary>Whether this kind grants the listing's featured flag.</summary>
    public bool GrantsFeaturedPlacement =>
        Kind is PromotionKind.Featured or PromotionKind.Spotlight;

    public static Result<ListingPromotion> Start(
        ListingPromotionId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        PromotionKind kind,
        DateTimeOffset startsAt,
        DateTimeOffset endsAt,
        DateTimeOffset now,
        UserId? grantedBy = null,
        string? externalReference = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(sellerId);

        if (!Enum.IsDefined(kind))
        {
            return Result.Failure<ListingPromotion>(MarketplaceErrors.PromotionWindowInvalid);
        }

        if (endsAt <= startsAt || endsAt <= now)
        {
            return Result.Failure<ListingPromotion>(MarketplaceErrors.PromotionWindowInvalid);
        }

        var promotion = new ListingPromotion(
            id, listingId, sellerId, kind, startsAt, endsAt, grantedBy, now)
        {
            ExternalReference = Clamp(externalReference)
        };

        promotion.RaiseDomainEvent(new ListingPromotionStartedDomainEvent
        {
            PromotionId = id,
            ListingId = listingId,
            SellerId = sellerId,
            Kind = kind,
            StartsAt = startsAt,
            EndsAt = endsAt,
            GrantedBy = grantedBy
        });

        return Result.Success(promotion);
    }

    /// <summary>Ends it early.</summary>
    public Result Cancel(UserId? actorId, DateTimeOffset now)
    {
        if (IsCancelled)
        {
            return Result.Success();
        }

        if (EndsAt <= now)
        {
            return Result.Failure(MarketplaceErrors.PromotionEnded);
        }

        IsCancelled = true;
        CancelledAt = now;
        CancelledBy = actorId;

        RaiseDomainEvent(new ListingPromotionEndedDomainEvent
        {
            PromotionId = Id,
            ListingId = ListingId,
            Kind = Kind,
            WasCancelled = true,
            EndedBy = actorId,
            EndedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Records that the window has closed. The sweep's entry point.
    /// <para>
    /// Raises the ending event so the listing's featured flag comes down. Without it the flag
    /// outlives the promotion, which is the exact failure a flag-on-the-listing design has.
    /// </para>
    /// </summary>
    public Result Conclude(DateTimeOffset now)
    {
        if (IsCancelled || EndsAt > now)
        {
            return Result.Success();
        }

        RaiseDomainEvent(new ListingPromotionEndedDomainEvent
        {
            PromotionId = Id,
            ListingId = ListingId,
            Kind = Kind,
            WasCancelled = false,
            EndedBy = null,
            EndedAt = now
        });

        return Result.Success();
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    private static string? Clamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > MarketplacePersistenceLimits.ExternalReference
            ? trimmed[..MarketplacePersistenceLimits.ExternalReference]
            : trimmed;
    }
}
