using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Events;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Domain.Memberships;

/// <summary>
/// One member's standing in one organization.
/// <para>
/// <b>Its own aggregate rather than a collection on <see cref="Organizations.Organization"/>, and
/// the reason is scale rather than tidiness.</b> A national association has tens of thousands of
/// members; loading them to render the organization's name would be absurd, and an administrator
/// approving a single request would take a write lock on the whole institution. Keyed by
/// organization and paged, this is a row per person and the organization holds only the counts.
/// </para>
/// <para>
/// <b>Role and status are orthogonal and both are kept.</b> Somebody can be an administrator who
/// has left, which is not a contradiction — it is what the record has to say to explain why they
/// were an administrator when the thing they are being asked about happened.
/// </para>
/// <para>
/// <b>Never hard-deleted, and not soft-deletable either.</b> A membership that vanished would erase
/// the history that makes readmission decisions possible; leaving is a status, and it is a status
/// precisely so the record survives. What a member can ask to have forgotten is their reason text,
/// which the removal path clears.
/// </para>
/// </summary>
public sealed class OrganizationMembership
    : AggregateRoot<OrganizationMembershipId>, IAuditableEntity
{
    private readonly List<MembershipHistoryEntry> _history = [];

    private OrganizationMembership(
        OrganizationMembershipId id,
        OrganizationId organizationId,
        UserId memberUserId,
        MembershipStatus status,
        MembershipRole role,
        DateTimeOffset createdAt) : base(id)
    {
        OrganizationId = organizationId;
        MemberUserId = memberUserId;
        Status = status;
        Role = role;
        CreatedAt = createdAt;
        RequestedAt = createdAt;

        if (status == MembershipStatus.Active)
        {
            JoinedAt = createdAt;
        }
    }

    private OrganizationMembership() { }

    public OrganizationId OrganizationId { get; private set; } = default!;

    public UserId MemberUserId { get; private set; } = default!;

    public MembershipStatus Status { get; private set; }

    public MembershipRole Role { get; private set; }

    /// <summary>
    /// What the member said when they asked to join. Read by the administrator deciding, and
    /// cleared when the membership ends.
    /// </summary>
    public string? RequestMessage { get; private set; }

    /// <summary>Who invited them, when they were invited rather than having asked.</summary>
    public UserId? InvitedBy { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? JoinedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public UserId? DecidedBy { get; private set; }

    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>
    /// When the membership lapses if it is not renewed. Null on the overwhelming majority of
    /// memberships, which do not expire.
    /// </summary>
    public DateTimeOffset? RenewsAt { get; private set; }

    /// <summary>
    /// How many events the member has attended under this organization. Settled from the RSVP
    /// handler, and the number an administrator sorts a volunteer roster by.
    /// </summary>
    public int AttendedEventCount { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<MembershipHistoryEntry> History =>
        _history.OrderBy(entry => entry.OccurredAt).ToList().AsReadOnly();

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    public bool IsCurrent => MembershipStatusTransitions.IsCurrent(Status);

    public bool IsPending => MembershipStatusTransitions.IsPending(Status);

    /// <summary>Whether the member holds at least the given role. The module's permission check.</summary>
    public bool HasAtLeast(MembershipRole role) => IsCurrent && Role >= role;

    /// <summary>Whether the member may run the organization's events.</summary>
    public bool CanOrganize => HasAtLeast(MembershipRole.Organizer);

    /// <summary>Whether the member may manage the page and its members.</summary>
    public bool CanAdminister => HasAtLeast(MembershipRole.Administrator);

    /// <summary>Whether the member may work a door. Volunteers can, and nothing more.</summary>
    public bool CanTakeAttendance => HasAtLeast(MembershipRole.Volunteer);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens a membership at whatever the organization's policy dictates.
    /// <para>
    /// The status is decided by the caller from the policy rather than here, because the policy
    /// lives on the organization and this aggregate deliberately does not hold one. What this
    /// enforces is that the opening status is one a membership can actually start in.
    /// </para>
    /// </summary>
    public static Result<OrganizationMembership> Open(
        OrganizationMembershipId id,
        OrganizationId organizationId,
        UserId memberUserId,
        MembershipStatus status,
        MembershipRole role,
        Guid historyEntryId,
        DateTimeOffset now,
        string? requestMessage = null,
        UserId? invitedBy = null,
        int? renewalMonths = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(organizationId);
        ArgumentNullException.ThrowIfNull(memberUserId);

        if (status is not (MembershipStatus.Active
            or MembershipStatus.Requested
            or MembershipStatus.Invited))
        {
            return Result.Failure<OrganizationMembership>(
                CommunityLifeErrors.MembershipTransitionInvalid);
        }

        var membership = new OrganizationMembership(
            id, organizationId, memberUserId, status, role, now)
        {
            RequestMessage = Clip(requestMessage),
            InvitedBy = invitedBy
        };

        if (status == MembershipStatus.Active && renewalMonths is { } months)
        {
            membership.RenewsAt = now.AddMonths(months);
        }

        membership._history.Add(MembershipHistoryEntry.Create(
            historyEntryId, status, role, invitedBy, null, now));

        membership.RaiseDomainEvent(new OrganizationMembershipStatusChangedDomainEvent
        {
            MembershipId = id,
            OrganizationId = organizationId,
            MemberUserId = memberUserId,
            PreviousStatus = status,
            CurrentStatus = status,
            Role = role,
            ChangedBy = invitedBy,
            ChangedAt = now
        });

        return Result.Success(membership);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The general lifecycle move. Every membership transition goes through here, so the machine is
    /// consulted once, the history gains exactly one entry, and the status event is raised once.
    /// </summary>
    public Result MoveTo(
        MembershipStatus target,
        Guid historyEntryId,
        DateTimeOffset now,
        UserId? changedBy = null,
        string? reason = null,
        int? renewalMonths = null)
    {
        if (Status == target)
        {
            return Result.Success();
        }

        if (!MembershipStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(CommunityLifeErrors.MembershipTransitionInvalid);
        }

        var previous = Status;

        Status = target;
        Revision++;
        ModifiedAt = now;

        switch (target)
        {
            case MembershipStatus.Active:
                JoinedAt ??= now;
                DecidedAt = now;
                DecidedBy = changedBy;
                EndedAt = null;
                RenewsAt = renewalMonths is { } months ? now.AddMonths(months) : null;
                break;

            case MembershipStatus.Rejected:
                DecidedAt = now;
                DecidedBy = changedBy;
                EndedAt = now;

                // The message was written for the administrator who has now decided. Keeping it
                // after a refusal serves nobody and is the kind of text a member would ask to have
                // forgotten.
                RequestMessage = null;
                break;

            case MembershipStatus.Left:
            case MembershipStatus.Removed:
                EndedAt = now;
                RenewsAt = null;
                RequestMessage = null;
                break;

            case MembershipStatus.Lapsed:
                EndedAt = now;
                RenewsAt = null;
                break;

            case MembershipStatus.Requested:
            case MembershipStatus.Invited:
                // Coming back after leaving. The dates reset; the history keeps both attempts.
                RequestedAt = now;
                EndedAt = null;
                DecidedAt = null;
                DecidedBy = null;
                break;
        }

        _history.Add(MembershipHistoryEntry.Create(
            historyEntryId, target, Role, changedBy, reason, now));

        RaiseDomainEvent(new OrganizationMembershipStatusChangedDomainEvent
        {
            MembershipId = Id,
            OrganizationId = OrganizationId,
            MemberUserId = MemberUserId,
            PreviousStatus = previous,
            CurrentStatus = target,
            Role = Role,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>Approves a pending request or a pending invitation.</summary>
    public Result Approve(
        Guid historyEntryId, DateTimeOffset now, UserId? approvedBy, int? renewalMonths = null)
    {
        if (!IsPending)
        {
            return Result.Failure(CommunityLifeErrors.MembershipNotPending);
        }

        return MoveTo(
            MembershipStatus.Active, historyEntryId, now, approvedBy, null, renewalMonths);
    }

    /// <summary>Refuses a pending request.</summary>
    public Result Reject(
        Guid historyEntryId, DateTimeOffset now, UserId? rejectedBy, string? reason)
    {
        if (!IsPending)
        {
            return Result.Failure(CommunityLifeErrors.MembershipNotPending);
        }

        return MoveTo(MembershipStatus.Rejected, historyEntryId, now, rejectedBy, reason);
    }

    /// <summary>
    /// The member walks away.
    /// <para>
    /// <b>Refused for the owner</b>, because an organization without one is an organization nobody
    /// can publish, edit, or hand on. The owner transfers first, which is a deliberate act with its
    /// own audit entry.
    /// </para>
    /// </summary>
    public Result Leave(Guid historyEntryId, DateTimeOffset now)
    {
        if (Role == MembershipRole.Owner)
        {
            return Result.Failure(CommunityLifeErrors.OwnerCannotLeave);
        }

        return MoveTo(MembershipStatus.Left, historyEntryId, now);
    }

    /// <summary>An administrator removes the member.</summary>
    public Result Remove(
        Guid historyEntryId, DateTimeOffset now, UserId? removedBy, string? reason)
    {
        if (Role == MembershipRole.Owner)
        {
            return Result.Failure(CommunityLifeErrors.OwnerCannotLeave);
        }

        return MoveTo(MembershipStatus.Removed, historyEntryId, now, removedBy, reason);
    }

    /// <summary>
    /// Changes the member's role.
    /// <para>
    /// Whether the caller may grant the role is not decided here — that is a comparison against the
    /// caller's own role, which this aggregate does not know. What it enforces is that a role is
    /// only meaningful on a membership that is live or on its way to being live.
    /// </para>
    /// <para>
    /// <b>A pending membership counts, and that is deliberate.</b> An invitation names the role it
    /// is offering — "come and be a volunteer" is a different invitation from "come and be an
    /// administrator" — so refusing to set one until the invitation is accepted would make every
    /// invitation an offer of ordinary membership, and quietly demote everybody a body invited back
    /// into the job they used to do.
    /// </para>
    /// </summary>
    public Result ChangeRole(MembershipRole role, DateTimeOffset now, UserId? changedBy)
    {
        if (!IsCurrent && !IsPending)
        {
            return Result.Failure(CommunityLifeErrors.MembershipTransitionInvalid);
        }

        if (Role == role)
        {
            return Result.Success();
        }

        var previous = Role;

        Role = role;
        Revision++;
        ModifiedAt = now;

        RaiseDomainEvent(new OrganizationMembershipRoleChangedDomainEvent
        {
            MembershipId = Id,
            OrganizationId = OrganizationId,
            MemberUserId = MemberUserId,
            PreviousRole = previous,
            CurrentRole = role,
            Status = Status,
            ChangedBy = changedBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>Extends the renewal window. Called when a subscription body takes a payment elsewhere.</summary>
    public void Renew(int renewalMonths, DateTimeOffset now)
    {
        RenewsAt = now.AddMonths(Math.Max(1, renewalMonths));
        ModifiedAt = now;
    }

    /// <summary>
    /// Records that the member attended one of the organization's events.
    /// <para>
    /// Deliberately does not bump the revision. Attendance settling while an administrator edits the
    /// member's role must not make the administrator's save fail.
    /// </para>
    /// </summary>
    public void RecordAttendance(DateTimeOffset now)
    {
        AttendedEventCount++;
        ModifiedAt = now;
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
