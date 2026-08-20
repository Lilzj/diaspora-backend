using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.Rsvps;

/// <summary>
/// One member's intention to attend one event, and everything that came of it.
/// <para>
/// <b>Its own aggregate rather than a collection on the event, and the reason is the same as
/// membership's.</b> A festival with four thousand responses would load four thousand rows to
/// render its title, and one member changing their mind would take a write lock on the whole event.
/// </para>
/// <para>
/// <b>The whole journey is kept, and that is the module's answer to "track complete history".</b>
/// Somebody who withdrew a fortnight in advance and somebody who did not turn up are both
/// "not present", and treating them the same would make an organizer's no-show rate meaningless and
/// would punish the member who did the right thing.
/// </para>
/// <para>
/// <b><see cref="PartySize"/> is not decoration.</b> "Me and three others" is how a family responds
/// to a naming ceremony, and an attendance model that counted heads one at a time would tell an
/// organizer to lay out a quarter of the chairs they need.
/// </para>
/// <para>
/// <b>Not soft-deletable.</b> An RSVP that vanished would take the attendance record with it and
/// leave the event's counters permanently wrong; withdrawing is a status, and it is a status
/// precisely so the counters can be settled from the transition.
/// </para>
/// </summary>
public sealed class EventRsvp : AggregateRoot<EventRsvpId>, IAuditableEntity
{
    private readonly List<RsvpHistoryEntry> _history = [];

    private EventRsvp(
        EventRsvpId id,
        CommunityEventId eventId,
        OrganizationId? organizationId,
        UserId attendeeUserId,
        RsvpStatus status,
        int partySize,
        DateTimeOffset createdAt) : base(id)
    {
        EventId = eventId;
        OrganizationId = organizationId;
        AttendeeUserId = attendeeUserId;
        Status = status;
        PartySize = partySize;
        DeclineReason = RsvpDeclineReason.None;
        CreatedAt = createdAt;
        RespondedAt = createdAt;
    }

    private EventRsvp() { }

    public CommunityEventId EventId { get; private set; } = default!;

    /// <summary>
    /// The organizing organization, cached from the event. Held here so "every response to anything
    /// my association ran" is one indexed query rather than a join through the event table.
    /// </summary>
    public OrganizationId? OrganizationId { get; private set; }

    public UserId AttendeeUserId { get; private set; } = default!;

    public RsvpStatus Status { get; private set; }

    /// <summary>How many people are coming, including the member. At least one.</summary>
    public int PartySize { get; private set; }

    /// <summary>What the attendee told the organizer — dietary needs, arriving late, bringing a cake.</summary>
    public string? Note { get; private set; }

    public RsvpDeclineReason DeclineReason { get; private set; }

    public DateTimeOffset RespondedAt { get; private set; }

    public DateTimeOffset? CheckedInAt { get; private set; }

    public CheckInMethod? CheckInMethod { get; private set; }

    /// <summary>Who worked the door. Null when the attendee confirmed their own arrival.</summary>
    public UserId? CheckedInBy { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<RsvpHistoryEntry> History =>
        _history.OrderBy(entry => entry.OccurredAt).ToList().AsReadOnly();

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Whether this response is currently holding places against the capacity.</summary>
    public bool HoldsCapacity => RsvpStatusTransitions.HoldsCapacity(Status);

    public bool IsCheckedIn => Status is RsvpStatus.CheckedIn or RsvpStatus.Attended;

    public bool IsTerminal => RsvpStatusTransitions.IsTerminal(Status);

    /// <summary>How many places this response occupies. Zero when it is not holding any.</summary>
    public int HeldPlaces => HoldsCapacity ? PartySize : 0;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records a first response.
    /// <para>
    /// The status is decided by the caller — Going when there is room, Waitlisted when there is not,
    /// Interested when that is what the member said — because whether there is room is a fact about
    /// the event, and this aggregate deliberately does not hold one.
    /// </para>
    /// </summary>
    public static Result<EventRsvp> Respond(
        EventRsvpId id,
        CommunityEventId eventId,
        OrganizationId? organizationId,
        UserId attendeeUserId,
        RsvpStatus status,
        int partySize,
        Guid historyEntryId,
        DateTimeOffset now,
        string? note = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(eventId);
        ArgumentNullException.ThrowIfNull(attendeeUserId);

        if (status is not (RsvpStatus.Interested or RsvpStatus.Going or RsvpStatus.Waitlisted))
        {
            return Result.Failure<EventRsvp>(CommunityLifeErrors.RsvpTransitionInvalid);
        }

        var rsvp = new EventRsvp(
            id, eventId, organizationId, attendeeUserId, status, Math.Max(1, partySize), now)
        {
            Note = Clip(note)
        };

        rsvp._history.Add(RsvpHistoryEntry.Create(historyEntryId, status, null, null, now));

        rsvp.RaiseDomainEvent(new EventRsvpCreatedDomainEvent
        {
            RsvpId = id,
            CommunityEventId = eventId,
            OrganizationId = organizationId,
            AttendeeUserId = attendeeUserId,
            Status = status,
            PartySize = rsvp.PartySize,
            RespondedAt = now
        });

        rsvp.RaiseDomainEvent(new EventRsvpStatusChangedDomainEvent
        {
            RsvpId = id,
            CommunityEventId = eventId,
            OrganizationId = organizationId,
            AttendeeUserId = attendeeUserId,

            // The synthetic "from" for a first response. The settlement handler reads it as
            // "nothing was held before", which is exactly right, and it means creation and change
            // travel down one code path rather than two that can disagree.
            PreviousStatus = RsvpStatus.Cancelled,
            CurrentStatus = status,
            PartySize = rsvp.PartySize,
            ChangedAt = now
        });

        return Result.Success(rsvp);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Journey
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The general transition. Every RSVP move goes through here, so the machine is consulted once,
    /// the history gains exactly one entry, and the settlement event is raised once.
    /// </summary>
    public Result MoveTo(
        RsvpStatus target,
        Guid historyEntryId,
        DateTimeOffset now,
        UserId? changedBy = null,
        string? reason = null,
        RsvpDeclineReason declineReason = RsvpDeclineReason.None)
    {
        if (Status == target)
        {
            return Result.Success();
        }

        if (!RsvpStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(CommunityLifeErrors.RsvpTransitionInvalid);
        }

        var previous = Status;

        Status = target;
        Revision++;
        ModifiedAt = now;

        if (target is RsvpStatus.Cancelled or RsvpStatus.NoShow or RsvpStatus.Attended)
        {
            EndedAt = now;
        }
        else
        {
            EndedAt = null;
            RespondedAt = now;
        }

        DeclineReason = target switch
        {
            RsvpStatus.Cancelled => declineReason == RsvpDeclineReason.None
                ? RsvpDeclineReason.WithdrawnByAttendee
                : declineReason,
            RsvpStatus.NoShow => RsvpDeclineReason.DidNotAttend,
            _ => RsvpDeclineReason.None
        };

        _history.Add(
            RsvpHistoryEntry.Create(historyEntryId, target, changedBy, reason, now));

        RaiseDomainEvent(new EventRsvpStatusChangedDomainEvent
        {
            RsvpId = Id,
            CommunityEventId = EventId,
            OrganizationId = OrganizationId,
            AttendeeUserId = AttendeeUserId,
            PreviousStatus = previous,
            CurrentStatus = target,
            PartySize = PartySize,
            DeclineReason = DeclineReason,
            ChangedBy = changedBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Changes how many people are coming.
    /// <para>
    /// A separate act from a status change and deliberately so: a family growing from two to four
    /// moves the event's counters without moving the RSVP's state, and folding the two together
    /// would mean every party-size edit produced a spurious status transition in the history.
    /// </para>
    /// </summary>
    public Result<int> ChangePartySize(int partySize, DateTimeOffset now)
    {
        if (partySize < 1)
        {
            return Result.Failure<int>(CommunityLifeErrors.CapacityInvalid);
        }

        if (IsTerminal)
        {
            return Result.Failure<int>(CommunityLifeErrors.RsvpTransitionInvalid);
        }

        var delta = partySize - PartySize;

        if (delta == 0)
        {
            return Result.Success(0);
        }

        PartySize = partySize;
        Revision++;
        ModifiedAt = now;

        return Result.Success(HoldsCapacity ? delta : 0);
    }

    /// <summary>Restates the note the attendee left for the organizer.</summary>
    public void SetNote(string? note, DateTimeOffset now)
    {
        Note = Clip(note);
        ModifiedAt = now;
    }

    /// <summary>
    /// Records that the attendee arrived.
    /// <para>
    /// <b>Only from <see cref="RsvpStatus.Going"/>.</b> Somebody who was merely interested has not
    /// taken a place, and checking them in without promoting them first would leave the going count
    /// and the checked-in count disagreeing — the counters are settled from the transition, and a
    /// transition that skipped a state would skip its settlement too.
    /// </para>
    /// </summary>
    public Result CheckIn(
        CheckInMethod method,
        Guid historyEntryId,
        DateTimeOffset now,
        UserId? recordedBy = null)
    {
        if (IsCheckedIn)
        {
            return Result.Failure(CommunityLifeErrors.AlreadyCheckedIn);
        }

        if (Status != RsvpStatus.Going)
        {
            return Result.Failure(CommunityLifeErrors.NotAttending);
        }

        var moved = MoveTo(RsvpStatus.CheckedIn, historyEntryId, now, recordedBy);

        if (moved.IsFailure)
        {
            return moved;
        }

        CheckedInAt = now;
        CheckInMethod = method;
        CheckedInBy = recordedBy;

        RaiseDomainEvent(new EventCheckInCompletedDomainEvent
        {
            RsvpId = Id,
            CommunityEventId = EventId,
            OrganizationId = OrganizationId,
            AttendeeUserId = AttendeeUserId,
            Method = method,
            RecordedBy = recordedBy,
            PartySize = PartySize,
            CheckedInAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Settles the response after the event.
    /// <para>
    /// Called by the sweep once the event ends. Somebody who came through the door becomes
    /// <see cref="RsvpStatus.Attended"/>; somebody who said they were coming and did not becomes
    /// <see cref="RsvpStatus.NoShow"/>; everybody else is left alone, because being merely
    /// interested and not turning up is not a failure to appear.
    /// </para>
    /// </summary>
    public Result Settle(Guid historyEntryId, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Success();
        }

        return Status switch
        {
            RsvpStatus.CheckedIn => MoveTo(RsvpStatus.Attended, historyEntryId, now),
            RsvpStatus.Going => MoveTo(
                RsvpStatus.NoShow,
                historyEntryId,
                now,
                declineReason: RsvpDeclineReason.DidNotAttend),
            _ => Result.Success()
        };
    }

    /// <summary>The attendee withdraws.</summary>
    public Result Withdraw(Guid historyEntryId, DateTimeOffset now, string? reason = null) =>
        MoveTo(
            RsvpStatus.Cancelled,
            historyEntryId,
            now,
            reason: reason,
            declineReason: RsvpDeclineReason.WithdrawnByAttendee);

    /// <summary>
    /// The event was called off.
    /// <para>
    /// Recorded with its own decline reason so it never counts against the member. Somebody whose
    /// event was cancelled did nothing wrong, and a reputation signal that could not tell the
    /// difference would punish them for the organizer's decision.
    /// </para>
    /// </summary>
    public Result ReleaseForCancelledEvent(Guid historyEntryId, DateTimeOffset now)
    {
        if (IsTerminal)
        {
            return Result.Success();
        }

        return MoveTo(
            RsvpStatus.Cancelled,
            historyEntryId,
            now,
            declineReason: RsvpDeclineReason.EventCancelled);
    }

    /// <summary>Promotes a waitlisted response when a place frees up.</summary>
    public Result Promote(Guid historyEntryId, DateTimeOffset now)
    {
        if (Status != RsvpStatus.Waitlisted)
        {
            return Result.Failure(CommunityLifeErrors.RsvpTransitionInvalid);
        }

        return MoveTo(RsvpStatus.Going, historyEntryId, now);
    }

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

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

    private static string? Clip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length > CommunityLifeLimits.MaxNoteLength
            ? trimmed[..CommunityLifeLimits.MaxNoteLength]
            : trimmed;
    }
}
