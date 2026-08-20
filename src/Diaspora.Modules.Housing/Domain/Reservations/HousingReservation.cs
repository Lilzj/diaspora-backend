using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Errors;
using Diaspora.Modules.Housing.Domain.Events;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Housing.Domain.Reservations;

/// <summary>
/// A hold on a home for a named member.
/// <para>
/// <b>The whole reason this aggregate exists is that "reserved" cannot be a flag.</b> A flag says
/// nothing about who holds it, how many units, until when, from what date, or what happened when
/// it ended — and every one of those is a question the two parties will have. It also cannot be
/// expired by a sweep, which means a member who stopped replying takes a home off the market
/// permanently.
/// </para>
/// <para>
/// <b>Every exit settles the units, and there are exactly four.</b> Occupation converts held units
/// to occupied ones; cancellation, refusal and expiry return them. There is no fifth, because a
/// fifth would be a room that is neither on the market nor lived in — which is how a housing
/// platform quietly stops being able to let a property at all.
/// </para>
/// <para>
/// <b>The hold itself lives on the listing, not here.</b> This aggregate records the agreement;
/// the units come off the listing's <c>HousingAvailability</c> in the same unit of work as this
/// reservation's creation. Holding them here would mean summing every open reservation to answer
/// "how many beds are left", which is the query a hostel page runs on every render.
/// </para>
/// <para>
/// <b>On the concurrency of that hold.</b> The transition is decided by a pure function on the
/// listing's availability and made durable by <c>IHousingUnitReservationGuard</c> in the
/// persistence layer. B10 recommended a guarded atomic counter primitive for exactly this and it
/// has not been built; per the B11 brief this module does not invent a private one, and the
/// remaining dependency is recorded in the readiness report.
/// </para>
/// </summary>
public sealed class HousingReservation
    : AggregateRoot<HousingReservationId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<ReservationHistoryEntry> _history = [];

    private HousingReservation(
        HousingReservationId id,
        HousingListingId listingId,
        LandlordProfileId landlordId,
        UserId seekerUserId,
        int units,
        ReservationTerms terms,
        HousingViewingId? viewingId,
        string? message,
        DateTimeOffset createdAt) : base(id)
    {
        ListingId = listingId;
        LandlordId = landlordId;
        SeekerUserId = seekerUserId;
        Units = units;
        Terms = terms;
        ViewingId = viewingId;
        Message = message;
        Status = HousingReservationStatus.Requested;
        CreatedAt = createdAt;
    }

    private HousingReservation() { }

    // ── Parties & subject ───────────────────────────────────────────────────────────────────

    public HousingListingId ListingId { get; private set; } = default!;

    public LandlordProfileId LandlordId { get; private set; } = default!;

    /// <summary>The member who wants to live there.</summary>
    public UserId SeekerUserId { get; private set; } = default!;

    /// <summary>How many units are held. One for a home, more for hostel beds.</summary>
    public int Units { get; private set; }

    public ReservationTerms Terms { get; private set; } = default!;

    /// <summary>
    /// The viewing this followed from, when there was one. Recorded rather than merely checked,
    /// because "we agreed this at the viewing on the 14th" is what a disagreement turns on.
    /// </summary>
    public HousingViewingId? ViewingId { get; private set; }

    public string? Message { get; private set; }

    // ── State ───────────────────────────────────────────────────────────────────────────────

    public HousingReservationStatus Status { get; private set; }

    public DateTimeOffset? AcceptedAt { get; private set; }

    public DateTimeOffset? LeaseSignedAt { get; private set; }

    public DateTimeOffset? OccupiedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>Who ended it. Null on an expiry, which the sweep recorded.</summary>
    public UserId? ClosedBy { get; private set; }

    public string? ClosureReason { get; private set; }

    /// <summary>Every step, in order.</summary>
    public IReadOnlyList<ReservationHistoryEntry> History =>
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

    /// <summary>Whether this reservation is still keeping units off the market.</summary>
    public bool HoldsUnits =>
        !IsDeleted && HousingReservationStatusTransitions.HoldsUnits(Status);

    /// <summary>Whether the hold has run out with nothing agreed.</summary>
    public bool HasLapsed(DateTimeOffset now) => HoldsUnits && Terms.HasLapsed(now);

    /// <summary>Whether a member is one of the two parties.</summary>
    public bool Involves(UserId userId, UserId landlordUserId) =>
        SeekerUserId == userId || landlordUserId == userId;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens a reservation.
    /// <para>
    /// The handler has already held the units on the listing, in the same unit of work. That
    /// ordering is deliberate: a reservation that existed for even a moment before its hold is a
    /// window in which a second member takes the same room.
    /// </para>
    /// </summary>
    public static Result<HousingReservation> Open(
        HousingReservationId id,
        HousingListingId listingId,
        LandlordProfileId landlordId,
        UserId seekerUserId,
        int units,
        ReservationTerms terms,
        DateTimeOffset now,
        Guid entryId,
        HousingViewingId? viewingId = null,
        string? message = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(landlordId);
        ArgumentNullException.ThrowIfNull(seekerUserId);
        ArgumentNullException.ThrowIfNull(terms);

        if (units < 1 || units > HousingLimits.MaxUnits)
        {
            return Result.Failure<HousingReservation>(HousingErrors.UnitsInvalid);
        }

        var reservation = new HousingReservation(
            id, listingId, landlordId, seekerUserId, units, terms, viewingId, Clamp(message), now);

        reservation.Record(
            entryId, HousingReservationStatus.Requested, seekerUserId, null, now);

        reservation.RaiseDomainEvent(new HousingReservationCreatedDomainEvent
        {
            ReservationId = id,
            ListingId = listingId,
            LandlordId = landlordId,
            SeekerUserId = seekerUserId,
            Units = units,
            MoveInDate = terms.MoveInDate,
            HoldExpiresAt = terms.HoldExpiresAt,
            ReservedAt = now
        });

        return Result.Success(reservation);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Transitions
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The landlord agrees to hold it.
    /// <para>
    /// Optionally restates the hold expiry, because accepting is exactly when a landlord says
    /// "I will keep it for you until Friday" — and forcing a separate call for that would leave
    /// the common case as two round trips with a half-arranged state in between.
    /// </para>
    /// </summary>
    public Result Accept(
        UserId actorId, DateTimeOffset? holdExpiresAt, DateTimeOffset now, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);

        var guard = EnsureTransition(HousingReservationStatus.Reserved);

        if (guard.IsFailure)
        {
            return guard;
        }

        var previous = Status;

        if (holdExpiresAt is not null)
        {
            Terms = Terms.WithHoldExpiry(holdExpiresAt);
        }

        Status = HousingReservationStatus.Reserved;
        AcceptedAt = now;

        Record(entryId, Status, actorId, null, now);
        RaiseStatusChanged(previous, Status, actorId, null, now);

        return Result.Success();
    }

    /// <summary>
    /// The parties report that a tenancy agreement exists.
    /// <para>
    /// <b>Housing stores the fact and never the document.</b> Lease management and digital
    /// signatures are explicitly out of scope; this is a milestone in a workflow, and the boundary
    /// between what this module tracks and what a future contracts module would own.
    /// </para>
    /// </summary>
    public Result RecordLeaseSigned(UserId actorId, DateTimeOffset now, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);

        var guard = EnsureTransition(HousingReservationStatus.LeaseSigned);

        if (guard.IsFailure)
        {
            return guard;
        }

        var previous = Status;

        Status = HousingReservationStatus.LeaseSigned;
        LeaseSignedAt = now;

        Record(entryId, Status, actorId, null, now);
        RaiseStatusChanged(previous, Status, actorId, null, now);

        return Result.Success();
    }

    /// <summary>
    /// Somebody moved in. The module's success terminal state, and the only exit that does not
    /// return units to the market.
    /// </summary>
    public Result RecordOccupied(UserId actorId, DateTimeOffset now, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);

        var guard = EnsureTransition(HousingReservationStatus.Occupied);

        if (guard.IsFailure)
        {
            return guard;
        }

        var previous = Status;

        Status = HousingReservationStatus.Occupied;
        OccupiedAt = now;
        ClosedAt = now;
        ClosedBy = actorId;

        Record(entryId, Status, actorId, null, now);
        RaiseStatusChanged(previous, Status, actorId, null, now);

        return Result.Success();
    }

    /// <summary>
    /// Either party calls it off. The units go back on the market.
    /// <para>
    /// <b>Available to both sides and never blocked.</b> A member who found somewhere else and a
    /// landlord whose previous tenant stayed on are both ordinary, and a reservation that could not
    /// be cancelled would hold a home hostage until its expiry.
    /// </para>
    /// </summary>
    public Result Cancel(UserId? actorId, string? reason, DateTimeOffset now, Guid entryId)
    {
        var guard = EnsureTransition(HousingReservationStatus.Cancelled);

        if (guard.IsFailure)
        {
            return guard;
        }

        return Close(HousingReservationStatus.Cancelled, actorId, reason, now, entryId);
    }

    /// <summary>The landlord refuses. Distinct from a cancellation so the record shows who ended it.</summary>
    public Result Decline(UserId actorId, string? reason, DateTimeOffset now, Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);

        var guard = EnsureTransition(HousingReservationStatus.Declined);

        if (guard.IsFailure)
        {
            return guard;
        }

        return Close(HousingReservationStatus.Declined, actorId, reason, now, entryId);
    }

    /// <summary>
    /// The hold ran out. The sweep's entry point.
    /// <para>
    /// Idempotent and quiet: a reservation that has already closed, or whose hold has not lapsed,
    /// succeeds and does nothing. That matters because the sweep runs on a timer against a work
    /// list that may be a few seconds stale.
    /// </para>
    /// </summary>
    public Result Expire(DateTimeOffset now, Guid entryId)
    {
        if (!HasLapsed(now))
        {
            return Result.Success();
        }

        return Close(HousingReservationStatus.Expired, null, null, now, entryId);
    }

    /// <summary>Extends the hold. What a landlord does when a member asks for another few days.</summary>
    public Result ExtendHold(DateTimeOffset holdExpiresAt, DateTimeOffset now)
    {
        if (!HoldsUnits)
        {
            return Result.Failure(HousingErrors.ReservationAlreadyClosed);
        }

        if (holdExpiresAt <= now)
        {
            return Result.Failure(HousingErrors.ReservationWindowInvalid);
        }

        Terms = Terms.WithHoldExpiry(holdExpiresAt);
        ModifiedAt = now;

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

    private Result Close(
        HousingReservationStatus status,
        UserId? actorId,
        string? reason,
        DateTimeOffset now,
        Guid entryId)
    {
        var previous = Status;
        var trimmed = Clamp(reason);

        Status = status;
        ClosedAt = now;
        ClosedBy = actorId;
        ClosureReason = trimmed;

        Record(entryId, status, actorId, trimmed, now);
        RaiseStatusChanged(previous, status, actorId, trimmed, now);

        return Result.Success();
    }

    private Result EnsureTransition(HousingReservationStatus target)
    {
        if (IsDeleted)
        {
            return Result.Failure(HousingErrors.ReservationNotFound);
        }

        if (HousingReservationStatusTransitions.IsClosed(Status))
        {
            return Result.Failure(HousingErrors.ReservationAlreadyClosed);
        }

        return HousingReservationStatusTransitions.CanTransition(Status, target)
            ? Result.Success()
            : Result.Failure(HousingErrors.ReservationTransitionInvalid);
    }

    private void Record(
        Guid entryId,
        HousingReservationStatus status,
        UserId? actorId,
        string? reason,
        DateTimeOffset now)
    {
        if (_history.Count >= HousingLimits.MaxHistoryEntries)
        {
            return;
        }

        _history.Add(ReservationHistoryEntry.Create(entryId, status, actorId, reason, now));
    }

    private void RaiseStatusChanged(
        HousingReservationStatus previous,
        HousingReservationStatus current,
        UserId? changedBy,
        string? reason,
        DateTimeOffset now) =>
        RaiseDomainEvent(new HousingReservationStatusChangedDomainEvent
        {
            ReservationId = Id,
            ListingId = ListingId,
            LandlordId = LandlordId,
            SeekerUserId = SeekerUserId,
            PreviousStatus = previous,
            CurrentStatus = current,
            Units = Units,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });

    private static string? Clamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > HousingLimits.MaxReservationNote
            ? trimmed[..HousingLimits.MaxReservationNote]
            : trimmed;
    }
}
