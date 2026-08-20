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

namespace Diaspora.Modules.Housing.Domain.Viewings;

/// <summary>
/// Somebody arranging to see a property.
/// <para>
/// <b>The aggregate B10 has no equivalent of, and the one that makes this a relocation module.</b>
/// Nobody takes a home from a photograph. A viewing is the step between finding a property and
/// committing to it, most viewings never become reservations, and a single flat may have ten of
/// them open at once — which is exactly why it cannot be a field on the listing and cannot hold a
/// unit. It is precisely because viewings are cheap that reservations can afford to be expensive.
/// </para>
/// <para>
/// <b>It is also the platform's answer to two hard problems at once.</b> A confirmed viewing is
/// what unlocks the property's full address, so booking one is the intended path to an address
/// rather than a workaround for one. And a landlord who repeatedly fails to attend viewings for a
/// property that does not exist is what an advance-fee rental fraud looks like from the outside —
/// which is why <see cref="HousingViewingStatus.NoShow"/> is recorded against a party rather than
/// folded into a cancellation.
/// </para>
/// <para>
/// <b>Virtual viewings are first-class.</b> Most of the members this module serves are choosing a
/// home from another country; a workflow that assumed physical attendance would exclude the whole
/// primary use case.
/// </para>
/// </summary>
public sealed class HousingViewing
    : AggregateRoot<HousingViewingId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<ViewingHistoryEntry> _history = [];

    private HousingViewing(
        HousingViewingId id,
        HousingListingId listingId,
        LandlordProfileId landlordId,
        UserId seekerUserId,
        ViewingWindow window,
        string? message,
        DateTimeOffset createdAt) : base(id)
    {
        ListingId = listingId;
        LandlordId = landlordId;
        SeekerUserId = seekerUserId;
        Window = window;
        Message = message;
        Status = HousingViewingStatus.Requested;
        CreatedAt = createdAt;
    }

    private HousingViewing() { }

    // ── Parties & subject ───────────────────────────────────────────────────────────────────

    public HousingListingId ListingId { get; private set; } = default!;

    public LandlordProfileId LandlordId { get; private set; } = default!;

    /// <summary>The member who wants to see it.</summary>
    public UserId SeekerUserId { get; private set; } = default!;

    public ViewingWindow Window { get; private set; } = default!;

    /// <summary>What the member said when they asked. Free text, shown to the landlord.</summary>
    public string? Message { get; private set; }

    // ── State ───────────────────────────────────────────────────────────────────────────────

    public HousingViewingStatus Status { get; private set; }

    public DateTimeOffset? RespondedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public UserId? ClosedBy { get; private set; }

    public string? ClosureReason { get; private set; }

    /// <summary>
    /// Which party failed to appear, on a no-show. Null in every other state.
    /// <para>
    /// Recorded because the two mean opposite things: a member who does not turn up wasted a
    /// landlord's Saturday, and a landlord who does not turn up may be advertising a property they
    /// have no connection to.
    /// </para>
    /// </summary>
    public bool? LandlordFailedToAttend { get; private set; }

    /// <summary>Every step, in order.</summary>
    public IReadOnlyList<ViewingHistoryEntry> History =>
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

    /// <summary>Whether the visit is still ahead of the parties.</summary>
    public bool IsOpen => !IsDeleted && HousingViewingStatusTransitions.IsOpen(Status);

    /// <summary>
    /// Whether this viewing entitles the member to the property's full address.
    /// <para>
    /// A scheduled viewing is enough, deliberately: a member cannot attend an address they have
    /// not been given, so waiting for completion would make the requirement unsatisfiable.
    /// </para>
    /// </summary>
    public bool UnlocksAddress =>
        !IsDeleted
        && Status is HousingViewingStatus.Scheduled
            or HousingViewingStatus.Completed
            or HousingViewingStatus.NoShow;

    /// <summary>Whether this satisfies a landlord's viewing-before-reservation requirement.</summary>
    public bool SatisfiesReservationRequirement =>
        !IsDeleted && HousingViewingStatusTransitions.SatisfiesViewingRequirement(Status);

    /// <summary>Whether the slot has passed without the viewing being closed.</summary>
    public bool HasLapsed(DateTimeOffset now) => IsOpen && Window.HasLapsed(now);

    /// <summary>Whether a member is one of the two parties.</summary>
    public bool Involves(UserId userId, UserId landlordUserId) =>
        SeekerUserId == userId || landlordUserId == userId;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<HousingViewing> Request(
        HousingViewingId id,
        HousingListingId listingId,
        LandlordProfileId landlordId,
        UserId seekerUserId,
        ViewingWindow window,
        string? message,
        DateTimeOffset now,
        Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(landlordId);
        ArgumentNullException.ThrowIfNull(seekerUserId);
        ArgumentNullException.ThrowIfNull(window);

        if (window.PreferredAt is { } preferred && preferred < now)
        {
            return Result.Failure<HousingViewing>(HousingErrors.ViewingSlotInPast);
        }

        var viewing = new HousingViewing(
            id, listingId, landlordId, seekerUserId, window, Clamp(message), now);

        viewing.Record(entryId, HousingViewingStatus.Requested, seekerUserId, null, now);

        viewing.RaiseDomainEvent(new HousingViewingRequestedDomainEvent
        {
            ViewingId = id,
            ListingId = listingId,
            LandlordId = landlordId,
            SeekerUserId = seekerUserId,
            PreferredAt = window.PreferredAt,
            IsVirtual = window.IsVirtual,
            RequestedAt = now
        });

        return Result.Success(viewing);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Transitions
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The landlord agrees a time.
    /// <para>
    /// This is the transition that unlocks the address, so it is the one a member is actually
    /// waiting for. The original request time is kept beside the confirmed one — see
    /// <see cref="ViewingWindow"/> for why.
    /// </para>
    /// </summary>
    public Result Schedule(
        UserId actorId,
        DateTimeOffset scheduledFor,
        string? place,
        DateTimeOffset now,
        Guid entryId)
    {
        ArgumentNullException.ThrowIfNull(actorId);

        var guard = EnsureTransition(HousingViewingStatus.Scheduled);

        if (guard.IsFailure)
        {
            return guard;
        }

        if (scheduledFor < now)
        {
            return Result.Failure(HousingErrors.ViewingSlotInPast);
        }

        var previous = Status;

        Window = Window.Confirm(scheduledFor, place);
        Status = HousingViewingStatus.Scheduled;
        RespondedAt = now;

        Record(entryId, Status, actorId, null, now);
        RaiseStatusChanged(previous, Status, actorId, null, now);

        return Result.Success();
    }

    /// <summary>The visit happened. The state a reservation may follow from.</summary>
    public Result Complete(UserId? actorId, DateTimeOffset now, Guid entryId)
    {
        var guard = EnsureTransition(HousingViewingStatus.Completed);

        if (guard.IsFailure)
        {
            return guard;
        }

        var previous = Status;

        Status = HousingViewingStatus.Completed;
        CompletedAt = now;
        ClosedAt = now;
        ClosedBy = actorId;

        Record(entryId, Status, actorId, null, now);
        RaiseStatusChanged(previous, Status, actorId, null, now);

        return Result.Success();
    }

    /// <summary>
    /// Nobody turned up.
    /// <para>
    /// <paramref name="landlordFailedToAttend"/> is required rather than inferred, because the
    /// distinction is the whole value of the record: one is a wasted afternoon and the other is a
    /// fraud signal.
    /// </para>
    /// </summary>
    public Result RecordNoShow(
        UserId? actorId,
        bool landlordFailedToAttend,
        string? reason,
        DateTimeOffset now,
        Guid entryId)
    {
        var guard = EnsureTransition(HousingViewingStatus.NoShow);

        if (guard.IsFailure)
        {
            return guard;
        }

        var previous = Status;
        var trimmed = Clamp(reason);

        Status = HousingViewingStatus.NoShow;
        LandlordFailedToAttend = landlordFailedToAttend;
        ClosedAt = now;
        ClosedBy = actorId;
        ClosureReason = trimmed;

        Record(entryId, Status, actorId, trimmed, now);
        RaiseStatusChanged(previous, Status, actorId, trimmed, now);

        return Result.Success();
    }

    /// <summary>
    /// Either party calls it off.
    /// <para>
    /// Available to both sides and never blocked, for the reason B10 gives about reservations: a
    /// member who changed their mind and a landlord whose boiler failed are both ordinary, and a
    /// viewing that could not be cancelled would leave somebody waiting outside a door.
    /// </para>
    /// </summary>
    public Result Cancel(UserId? actorId, string? reason, DateTimeOffset now, Guid entryId)
    {
        var guard = EnsureTransition(HousingViewingStatus.Cancelled);

        if (guard.IsFailure)
        {
            return guard;
        }

        var previous = Status;
        var trimmed = Clamp(reason);

        Status = HousingViewingStatus.Cancelled;
        ClosedAt = now;
        ClosedBy = actorId;
        ClosureReason = trimmed;

        // A landlord declining an unanswered request is still a response, and the record must
        // show it. Otherwise a prompt refusal counts against them exactly as silence does.
        RespondedAt ??= previous == HousingViewingStatus.Requested ? now : RespondedAt;

        Record(entryId, Status, actorId, trimmed, now);
        RaiseStatusChanged(previous, Status, actorId, trimmed, now);

        return Result.Success();
    }

    /// <summary>
    /// The slot passed with nothing recorded. The sweep's entry point.
    /// <para>
    /// Idempotent and quiet: a viewing that has already closed, or whose slot has not passed,
    /// succeeds and does nothing.
    /// </para>
    /// </summary>
    public Result Expire(DateTimeOffset now, Guid entryId)
    {
        if (!HasLapsed(now))
        {
            return Result.Success();
        }

        var previous = Status;

        Status = HousingViewingStatus.Expired;
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

    private Result EnsureTransition(HousingViewingStatus target)
    {
        if (IsDeleted)
        {
            return Result.Failure(HousingErrors.ViewingNotFound);
        }

        if (HousingViewingStatusTransitions.IsClosed(Status))
        {
            return Result.Failure(HousingErrors.ViewingAlreadyClosed);
        }

        return HousingViewingStatusTransitions.CanTransition(Status, target)
            ? Result.Success()
            : Result.Failure(HousingErrors.ViewingTransitionInvalid);
    }

    private void Record(
        Guid entryId,
        HousingViewingStatus status,
        UserId? actorId,
        string? reason,
        DateTimeOffset now)
    {
        if (_history.Count >= HousingLimits.MaxHistoryEntries)
        {
            return;
        }

        _history.Add(ViewingHistoryEntry.Create(entryId, status, actorId, reason, now));
    }

    private void RaiseStatusChanged(
        HousingViewingStatus previous,
        HousingViewingStatus current,
        UserId? changedBy,
        string? reason,
        DateTimeOffset now) =>
        RaiseDomainEvent(new HousingViewingStatusChangedDomainEvent
        {
            ViewingId = Id,
            ListingId = ListingId,
            LandlordId = LandlordId,
            SeekerUserId = SeekerUserId,
            PreviousStatus = previous,
            CurrentStatus = current,
            ScheduledFor = Window.ConfirmedAt,
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

        return trimmed.Length > HousingLimits.MaxViewingNote
            ? trimmed[..HousingLimits.MaxViewingNote]
            : trimmed;
    }
}
