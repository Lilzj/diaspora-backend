using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Errors;
using Diaspora.Modules.Marketplace.Domain.Events;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Marketplace.Domain.Reservations;

/// <summary>
/// A hold on stock for a named buyer.
/// <para>
/// <b>The whole reason this aggregate exists is that "reserved" cannot be a flag.</b> A flag says
/// nothing about who holds it, how many units, until when, or what happened when it ended — and
/// every one of those is a question a seller and a buyer will have. It also cannot be expired by a
/// sweep, which means a buyer who never turned up takes the item off the market permanently.
/// </para>
/// <para>
/// <b>Every exit settles the stock, and there are exactly three.</b> Completing converts held
/// units to sold; cancelling and expiring return them. There is no fourth, because a fourth would
/// be units that are neither on the market nor accounted for — which is how a marketplace quietly
/// loses inventory.
/// </para>
/// <para>
/// <b>The hold itself lives on the listing, not here.</b> This aggregate records the agreement;
/// the units come off the listing's <c>ListingInventory</c> in the same unit of work as the
/// reservation's creation. Holding it here would mean summing every open reservation to answer
/// "how many are left", which is the query a listing page runs on every render.
/// </para>
/// </summary>
public sealed class ListingReservation
    : AggregateRoot<ListingReservationId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<ReservationEvent> _history = [];

    private ListingReservation(
        ListingReservationId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        UserId buyerUserId,
        int quantity,
        CollectionWindow window,
        ListingOfferId? offerId,
        decimal? agreedAmount,
        string? currencyCode,
        DateTimeOffset createdAt)
        : base(id)
    {
        ListingId = listingId;
        SellerId = sellerId;
        BuyerUserId = buyerUserId;
        Quantity = quantity;
        Window = window;
        OfferId = offerId;
        AgreedAmount = agreedAmount;
        CurrencyCode = currencyCode;
        Status = ReservationStatus.Pending;
        CreatedAt = createdAt;
    }

    private ListingReservation() { }

    // ── Parties & subject ───────────────────────────────────────────────────────────────────

    public MarketplaceListingId ListingId { get; private set; } = default!;

    public SellerProfileId SellerId { get; private set; } = default!;

    public UserId BuyerUserId { get; private set; } = default!;

    /// <summary>How many units are held.</summary>
    public int Quantity { get; private set; }

    /// <summary>When and where the hand-off is meant to happen.</summary>
    public CollectionWindow Window { get; private set; } = default!;

    /// <summary>
    /// The negotiation this came out of, when it came out of one. Null on a straight reservation
    /// at the asking price.
    /// </summary>
    public ListingOfferId? OfferId { get; private set; }

    /// <summary>
    /// What the parties agreed. Recorded because the listing's asking price may change afterwards
    /// and the sale was at this figure — the one number a dispute turns on.
    /// </summary>
    public decimal? AgreedAmount { get; private set; }

    public string? CurrencyCode { get; private set; }

    // ── State ───────────────────────────────────────────────────────────────────────────────

    public ReservationStatus Status { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Who ended it. Null on an expiry, which the sweep recorded.</summary>
    public UserId? ClosedBy { get; private set; }

    public string? ClosureReason { get; private set; }

    // ── History ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Every step, in order. The brief's reservation history.</summary>
    public IReadOnlyList<ReservationEvent> History =>
        _history.OrderBy(entry => entry.OccurredAt).ToList().AsReadOnly();

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Whether the reservation is still holding stock.</summary>
    public bool IsHoldingStock =>
        !IsDeleted && ReservationStatusTransitions.IsHoldingStock(Status);

    /// <summary>Whether the collection window has closed with the item uncollected.</summary>
    public bool HasLapsed(DateTimeOffset now) => IsHoldingStock && Window.HasLapsed(now);

    /// <summary>Whether a member is one of the two parties.</summary>
    public bool Involves(UserId userId, UserId sellerUserId) =>
        BuyerUserId == userId || sellerUserId == userId;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens a reservation.
    /// <para>
    /// The handler has already held the stock on the listing, in the same unit of work. That
    /// ordering is deliberate: a reservation that existed for even a moment before its hold is a
    /// window in which a second buyer takes the same item.
    /// </para>
    /// </summary>
    public static Result<ListingReservation> Open(
        ListingReservationId id,
        MarketplaceListingId listingId,
        SellerProfileId sellerId,
        UserId buyerUserId,
        int quantity,
        CollectionWindow window,
        DateTimeOffset now,
        Guid entryId,
        ListingOfferId? offerId = null,
        decimal? agreedAmount = null,
        string? currencyCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(sellerId);
        ArgumentNullException.ThrowIfNull(buyerUserId);
        ArgumentNullException.ThrowIfNull(window);

        if (quantity < 1 || quantity > MarketplaceLimits.MaxQuantity)
        {
            return Result.Failure<ListingReservation>(MarketplaceErrors.QuantityInvalid);
        }

        if (window.From is { } from && window.Until is { } until && until <= from)
        {
            return Result.Failure<ListingReservation>(MarketplaceErrors.ReservationWindowInvalid);
        }

        var reservation = new ListingReservation(
            id, listingId, sellerId, buyerUserId, quantity, window, offerId,
            agreedAmount, ListingPrice.NormalizeCurrency(currencyCode), now);

        reservation.Record(entryId, ReservationStatus.Pending, buyerUserId, null, now);

        reservation.RaiseDomainEvent(new ReservationCreatedDomainEvent
        {
            ReservationId = id,
            ListingId = listingId,
            SellerId = sellerId,
            BuyerUserId = buyerUserId,
            Quantity = quantity,
            ExpiresAt = window.Until,
            ReservedAt = now
        });

        return Result.Success(reservation);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Transitions
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The seller agrees and the hand-off is now expected.
    /// <para>
    /// Optionally restates the collection window, because acknowledging is exactly when a seller
    /// says "come Saturday between ten and two" — and forcing a separate call for it would leave
    /// the common case as two round trips with a half-arranged state in between.
    /// </para>
    /// </summary>
    public Result Acknowledge(
        UserId actorId, CollectionWindow? window, DateTimeOffset now, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);

        var guard = EnsureTransition(ReservationStatus.AwaitingCollection);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (window is not null)
        {
            Window = window;
        }

        Status = ReservationStatus.AwaitingCollection;
        AcknowledgedAt = now;

        Record(entryId, Status, actorId, null, now);
        RaiseStatusChanged(ReservationStatus.Pending, Status, actorId, null, now);

        return Result.Success();
    }

    /// <summary>
    /// The item changed hands.
    /// <para>
    /// The handler settles the listing's stock from the event this raises, which is the one place
    /// held units become sold units.
    /// </para>
    /// </summary>
    public Result Complete(UserId actorId, DateTimeOffset now, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);

        var guard = EnsureTransition(ReservationStatus.Completed);

        if (guard.IsFailure)
        {
            return guard;
        }

        var previous = Status;

        Status = ReservationStatus.Completed;
        CompletedAt = now;
        ClosedAt = now;
        ClosedBy = actorId;

        Record(entryId, Status, actorId, null, now);
        RaiseStatusChanged(previous, Status, actorId, null, now);

        return Result.Success();
    }

    /// <summary>
    /// Either party calls it off. The stock goes back on the shelf.
    /// <para>
    /// <b>Available to both sides and never blocked.</b> A buyer who changed their mind and a
    /// seller who broke the item are both ordinary, and a reservation that could not be cancelled
    /// would hold stock hostage until its window lapsed.
    /// </para>
    /// </summary>
    public Result Cancel(UserId? actorId, string? reason, DateTimeOffset now, Guid entryId)
    {
        var guard = EnsureTransition(ReservationStatus.Cancelled);

        if (guard.IsFailure)
        {
            return guard;
        }

        var previous = Status;
        var trimmed = Clamp(reason);

        Status = ReservationStatus.Cancelled;
        ClosedAt = now;
        ClosedBy = actorId;
        ClosureReason = trimmed;

        Record(entryId, Status, actorId, trimmed, now);
        RaiseStatusChanged(previous, Status, actorId, trimmed, now);

        return Result.Success();
    }

    /// <summary>
    /// The collection window closed with the item uncollected. The sweep's entry point.
    /// <para>
    /// Idempotent and quiet: a reservation that has already closed, or whose window has not
    /// lapsed, succeeds and does nothing.
    /// </para>
    /// </summary>
    public Result Expire(DateTimeOffset now, Guid entryId)
    {
        if (!HasLapsed(now))
        {
            return Result.Success();
        }

        var previous = Status;

        Status = ReservationStatus.Expired;
        ClosedAt = now;

        Record(entryId, Status, null, null, now);
        RaiseStatusChanged(previous, Status, null, null, now);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
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

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    private Result EnsureTransition(ReservationStatus target)
    {
        if (IsDeleted)
        {
            return Result.Failure(MarketplaceErrors.ReservationNotFound);
        }

        if (ReservationStatusTransitions.IsClosed(Status))
        {
            return Result.Failure(MarketplaceErrors.ReservationAlreadyClosed);
        }

        return ReservationStatusTransitions.CanTransition(Status, target)
            ? Result.Success()
            : Result.Failure(MarketplaceErrors.ReservationTransitionInvalid);
    }

    private void Record(
        Guid entryId,
        ReservationStatus status,
        UserId? actorId,
        string? reason,
        DateTimeOffset now)
    {
        if (_history.Count >= MarketplaceLimits.MaxReservationHistory)
        {
            return;
        }

        _history.Add(ReservationEvent.Create(entryId, status, actorId, reason, now));
    }

    private void RaiseStatusChanged(
        ReservationStatus previous,
        ReservationStatus current,
        UserId? changedBy,
        string? reason,
        DateTimeOffset now) =>
        RaiseDomainEvent(new ReservationStatusChangedDomainEvent
        {
            ReservationId = Id,
            ListingId = ListingId,
            SellerId = SellerId,
            BuyerUserId = BuyerUserId,
            PreviousStatus = previous,
            CurrentStatus = current,
            Quantity = Quantity,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });

    private static string? Clamp(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return null;
        }

        var trimmed = reason.Trim();

        return trimmed.Length > MarketplaceLimits.MaxReservationNote
            ? trimmed[..MarketplaceLimits.MaxReservationNote]
            : trimmed;
    }
}
