using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Memberships;

/// <summary>
/// One member's paid standing in one organization's programme, across fourteen states.
/// <para>
/// <b>Not B12's <c>OrganizationMembership</c>, and the two are orthogonal on purpose.</b> B12's edge
/// answers "does this person belong, and what may they do" — it is the authorization fact, and it is
/// what B18's capability set is projected from. This aggregate answers "which tier did they buy, and
/// where is that in its life". Somebody can be an Owner with no programme membership, and somebody
/// can hold a lapsed programme membership and no role at all. Conflating them would make cancelling
/// a subscription remove an administrator.
/// </para>
/// <para>
/// <b>Never deleted, and not soft-deletable either.</b> A membership that vanished would erase the
/// record of a payment somebody made. Leaving is
/// <see cref="ProgrammeMembershipStatus.Cancelled"/>; lapsing is
/// <see cref="ProgrammeMembershipStatus.Expired"/>. Both are states, and they are states precisely
/// so the record survives them.
/// </para>
/// <para>
/// <b>Two idempotency keys, and both are B15's primitive rather than a new one.</b>
/// <see cref="ApplicationIdempotencyKey"/> and <see cref="RenewalIdempotencyKey"/> are nullable
/// strings on the aggregate, each uniquely indexed, looked up before the write and replayed on a
/// repeat — exactly what B15 does for payment intents, purchases, subscriptions, donations and
/// contributions. Two columns because they are two operations: a member who applies and later renews
/// sends two different keys, and one column would make the renewal look like a replayed application.
/// </para>
/// </summary>
public sealed class ProgrammeMembership
    : AggregateRoot<ProgrammeMembershipId>, IAuditableEntity
{
    private readonly List<MembershipHistoryEntry> _history = [];

    private ProgrammeMembership(
        ProgrammeMembershipId id,
        Guid organizationId,
        MembershipTierId tierId,
        UserId memberUserId,
        ProgrammeMembershipStatus status,
        DateTimeOffset now) : base(id)
    {
        OrganizationId = organizationId;
        TierId = tierId;
        MemberUserId = memberUserId;
        Status = status;
        CreatedAt = now;
        SubmittedAt = status == ProgrammeMembershipStatus.Draft ? null : now;
    }

    private ProgrammeMembership() { }

    /// <summary>B12's organization. Never a copy of its name.</summary>
    public Guid OrganizationId { get; private set; }

    public MembershipTierId TierId { get; private set; } = default!;

    public UserId MemberUserId { get; private set; } = default!;

    public ProgrammeMembershipStatus Status { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public UserId? DecidedBy { get; private set; }

    /// <summary>Surfaced to the member verbatim. Cleared when the membership ends.</summary>
    public string? DecisionReason { get; private set; }

    public DateTimeOffset? JoinedAt { get; private set; }

    /// <summary>
    /// When the membership lapses if it is not renewed. Null on a one-off or lifetime tier.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>
    /// When the member should be asked to renew — the expiry, less the grace window.
    /// <para>
    /// Stored rather than derived, so the sweep can find work with an indexed comparison rather than
    /// by loading every membership and doing arithmetic on each.
    /// </para>
    /// </summary>
    public DateTimeOffset? RenewalDueAt { get; private set; }

    /// <summary>
    /// The B15 payment intent that paid for this, when one was needed.
    /// <para>
    /// <b>An identifier and nothing else.</b> No amount, no status, no receipt — every one of those
    /// is read back from B15, which is the only thing entitled to say whether money moved. Binding a
    /// settled payment to a membership is COMMUNITY-LIFE-GAP-003 and belongs to B15.1; this column is
    /// where that binding will land, and until it does it stays null.
    /// </para>
    /// </summary>
    public Guid? PaymentIntentId { get; private set; }

    /// <summary>B15's idempotency primitive, for the application. Uniquely indexed.</summary>
    public string? ApplicationIdempotencyKey { get; private set; }

    /// <summary>B15's idempotency primitive, for the most recent renewal. Uniquely indexed.</summary>
    public string? RenewalIdempotencyKey { get; private set; }

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

    public bool GrantsBenefits => ProgrammeMembershipTransitions.GrantsBenefits(Status);

    public bool OccupiesCapacity => ProgrammeMembershipTransitions.OccupiesCapacity(Status);

    public bool IsTerminal => ProgrammeMembershipTransitions.IsTerminal(Status);

    /// <summary>
    /// Whether the member may renew from where they are.
    /// <para>
    /// Server-decided, and the register is explicit that the client does not compare dates to reach
    /// it. A lifetime membership answers false because there is nothing to renew, not because a date
    /// has not arrived.
    /// </para>
    /// </summary>
    public bool IsRenewable =>
        ExpiresAt is not null
        && Status is ProgrammeMembershipStatus.Active
            or ProgrammeMembershipStatus.RenewalDue
            or ProgrammeMembershipStatus.Expired;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Opens an application.
    /// <para>
    /// The opening status is decided by the caller from the programme's policy — a programme that
    /// reviews applications opens at <see cref="ProgrammeMembershipStatus.Pending"/>, one that does
    /// not opens at <see cref="ProgrammeMembershipStatus.Approved"/> — because the policy lives on
    /// the programme and this aggregate deliberately does not hold one. What this enforces is that
    /// the opening status is one a membership can actually start in.
    /// </para>
    /// </summary>
    public static Result<ProgrammeMembership> Apply(
        ProgrammeMembershipId id,
        Guid organizationId,
        MembershipTierId tierId,
        UserId memberUserId,
        ProgrammeMembershipStatus openingStatus,
        Guid historyEntryId,
        DateTimeOffset now,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(tierId);
        ArgumentNullException.ThrowIfNull(memberUserId);

        if (openingStatus is not (ProgrammeMembershipStatus.Draft
            or ProgrammeMembershipStatus.Pending
            or ProgrammeMembershipStatus.Approved))
        {
            return Result.Failure<ProgrammeMembership>(
                CommunityProgrammeErrors.MembershipTransitionInvalid);
        }

        var membership = new ProgrammeMembership(
            id, organizationId, tierId, memberUserId, openingStatus, now)
        {
            ApplicationIdempotencyKey = MembershipProgramme.Clip(
                idempotencyKey, CommunityProgrammeLimits.MaxIdempotencyKeyLength)
        };

        membership._history.Add(MembershipHistoryEntry.Create(
            historyEntryId, openingStatus, MembershipActor.Member, null, now));

        membership.RaiseDomainEvent(new ProgrammeMembershipStatusChangedDomainEvent
        {
            MembershipId = id,
            OrganizationId = organizationId,
            TierId = tierId,
            MemberUserId = memberUserId,
            PreviousStatus = openingStatus,
            CurrentStatus = openingStatus,
            Actor = MembershipActor.Member,
            ChangedAt = now
        });

        return Result.Success(membership);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The one lifecycle move.
    /// <para>
    /// Every transition goes through here, so the table is consulted once, the history gains exactly
    /// one entry, and the status event is raised once. A handler that moved a membership by assigning
    /// <see cref="Status"/> would bypass all three, which is why the setter is private.
    /// </para>
    /// <para>
    /// <b>A move to the state it is already in succeeds and does nothing.</b> That is what makes the
    /// sweep idempotent: a second pass over a membership it already expired is a no-op rather than a
    /// second history entry and a second notification.
    /// </para>
    /// </summary>
    public Result MoveTo(
        ProgrammeMembershipStatus target,
        Guid historyEntryId,
        DateTimeOffset now,
        MembershipActor actor,
        UserId? changedBy = null,
        string? reason = null,
        int? renewalMonths = null)
    {
        if (Status == target)
        {
            return Result.Success();
        }

        if (!ProgrammeMembershipTransitions.CanTransition(Status, target))
        {
            return Result.Failure(CommunityProgrammeErrors.MembershipTransitionInvalid);
        }

        var previous = Status;

        Status = target;
        Revision++;
        ModifiedAt = now;

        switch (target)
        {
            case ProgrammeMembershipStatus.Pending:
                SubmittedAt ??= now;
                break;

            case ProgrammeMembershipStatus.Approved:
                DecidedAt = now;
                DecidedBy = changedBy;
                DecisionReason = MembershipProgramme.Clip(
                    reason, CommunityProgrammeLimits.MaxReasonLength);
                break;

            case ProgrammeMembershipStatus.Active:
                JoinedAt ??= now;
                DecidedAt ??= now;
                SetRenewalWindow(now, renewalMonths);
                break;

            case ProgrammeMembershipStatus.RenewalDue:
                // The dates are already set; the sweep only names the state.
                break;

            case ProgrammeMembershipStatus.Rejected:
                DecidedAt = now;
                DecidedBy = changedBy;
                DecisionReason = MembershipProgramme.Clip(
                    reason, CommunityProgrammeLimits.MaxReasonLength);
                break;

            case ProgrammeMembershipStatus.Suspended:
                DecidedBy = changedBy;
                DecisionReason = MembershipProgramme.Clip(
                    reason, CommunityProgrammeLimits.MaxReasonLength);
                break;

            case ProgrammeMembershipStatus.Cancelled:
            case ProgrammeMembershipStatus.Expired:
                RenewalDueAt = null;
                DecisionReason = MembershipProgramme.Clip(
                    reason, CommunityProgrammeLimits.MaxReasonLength);
                break;
        }

        _history.Add(MembershipHistoryEntry.Create(
            historyEntryId, target, actor, reason, now));

        RaiseDomainEvent(new ProgrammeMembershipStatusChangedDomainEvent
        {
            MembershipId = Id,
            OrganizationId = OrganizationId,
            TierId = TierId,
            MemberUserId = MemberUserId,
            PreviousStatus = previous,
            CurrentStatus = target,
            Actor = actor,
            Reason = reason,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// A member coming back after a membership ended.
    /// <para>
    /// <b>The same row, and a fresh chapter in its history.</b> The unique index on
    /// (organization, member) means there cannot be a second row, and reusing this one is what keeps
    /// the record of the first membership readable — which is exactly what an administrator deciding
    /// whether to readmit somebody needs to see.
    /// </para>
    /// <para>
    /// The tier may change on the way back in: somebody who let a Gold membership lapse may rejoin at
    /// Standard, and refusing that would make rejoining harder than joining.
    /// </para>
    /// </summary>
    public Result Rejoin(
        MembershipTierId tierId,
        ProgrammeMembershipStatus openingStatus,
        Guid historyEntryId,
        DateTimeOffset now,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(tierId);

        if (!ProgrammeMembershipTransitions.AllowsReapplication(Status))
        {
            return Result.Failure(CommunityProgrammeErrors.MembershipAlreadyExists);
        }

        if (openingStatus is not (ProgrammeMembershipStatus.Pending
            or ProgrammeMembershipStatus.Approved))
        {
            return Result.Failure(CommunityProgrammeErrors.MembershipTransitionInvalid);
        }

        TierId = tierId;
        DecisionReason = null;
        DecidedAt = null;
        DecidedBy = null;
        SubmittedAt = now;

        // A new application carries a new key. Clearing the old one is what lets the member's second
        // attempt be idempotent on its own terms rather than replaying the first.
        ApplicationIdempotencyKey = MembershipProgramme.Clip(
            idempotencyKey, CommunityProgrammeLimits.MaxIdempotencyKeyLength);

        return MoveTo(openingStatus, historyEntryId, now, MembershipActor.Member);
    }

    /// <summary>
    /// Extends the membership by another billing period.
    /// <para>
    /// <b>Records the key before doing anything else.</b> A renewal that extended the window and then
    /// failed to record its key would be renewed twice by the client's ordinary retry — which is the
    /// exact failure the register's §52 exists to prevent.
    /// </para>
    /// </summary>
    public Result Renew(
        Guid historyEntryId,
        DateTimeOffset now,
        int? renewalMonths,
        string? idempotencyKey)
    {
        if (!IsRenewable)
        {
            return Result.Failure(CommunityProgrammeErrors.MembershipNotRenewable);
        }

        RenewalIdempotencyKey = MembershipProgramme.Clip(
            idempotencyKey, CommunityProgrammeLimits.MaxIdempotencyKeyLength);

        // Renewing from Expired is not a transition — Expired is terminal — so the caller opens a
        // fresh membership instead. Renewing from Active or RenewalDue extends in place, and the
        // move to Active is what resets the window.
        if (Status == ProgrammeMembershipStatus.RenewalDue)
        {
            return MoveTo(
                ProgrammeMembershipStatus.Active,
                historyEntryId,
                now,
                MembershipActor.Member,
                renewalMonths: renewalMonths);
        }

        // Already Active: extend from the existing expiry rather than from now, so renewing early
        // does not cost the member the days they had left.
        var anchor = ExpiresAt is { } expiry && expiry > now ? expiry : now;

        SetRenewalWindow(anchor, renewalMonths);
        Revision++;
        ModifiedAt = now;

        _history.Add(MembershipHistoryEntry.Create(
            historyEntryId, ProgrammeMembershipStatus.Active, MembershipActor.Member, null, now));

        return Result.Success();
    }

    /// <summary>
    /// Records which B15 payment paid for this membership.
    /// <para>
    /// The identifier only. Whether it settled is B15's answer, read back from B15, and this module
    /// never infers activation from the presence of this value.
    /// </para>
    /// </summary>
    public void AttachPayment(Guid paymentIntentId, DateTimeOffset now)
    {
        PaymentIntentId = paymentIntentId;
        ModifiedAt = now;
    }

    /// <summary>Whether this membership has passed its renewal date as at the given moment.</summary>
    public bool IsPastRenewal(DateTimeOffset asOf) =>
        RenewalDueAt is { } due && due <= asOf;

    /// <summary>Whether this membership has passed its expiry as at the given moment.</summary>
    public bool IsPastExpiry(DateTimeOffset asOf) =>
        ExpiresAt is { } expiry && expiry <= asOf;

    private void SetRenewalWindow(DateTimeOffset anchor, int? renewalMonths)
    {
        if (renewalMonths is not { } months || months <= 0)
        {
            // A lifetime or one-off tier. Nothing expires and nothing is due.
            ExpiresAt = null;
            RenewalDueAt = null;

            return;
        }

        ExpiresAt = anchor.AddMonths(months);
        RenewalDueAt = ExpiresAt.Value.AddDays(-CommunityProgrammeLimits.RenewalGraceDays);
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
}

/// <summary>
/// One step in a membership's life.
/// <para>
/// <b>Who moved it is a category rather than an identifier.</b> An organization's decision is
/// recorded as <see cref="MembershipActor.Organization"/> and not as the administrator who made it:
/// the member is entitled to know the organization refused them, and is not entitled to know which
/// person in it clicked the button. The identifier is kept on the membership's
/// <c>DecidedBy</c> for the audit trail, which is not part of the member's own read.
/// </para>
/// </summary>
public sealed class MembershipHistoryEntry
{
    private MembershipHistoryEntry(
        Guid id,
        ProgrammeMembershipStatus status,
        MembershipActor actor,
        string? reason,
        DateTimeOffset occurredAt)
    {
        Id = id;
        Status = status;
        Actor = actor;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    private MembershipHistoryEntry() { }

    public Guid Id { get; private set; }

    public ProgrammeMembershipStatus Status { get; private set; }

    public MembershipActor Actor { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static MembershipHistoryEntry Create(
        Guid id,
        ProgrammeMembershipStatus status,
        MembershipActor actor,
        string? reason,
        DateTimeOffset occurredAt) =>
        new(
            id,
            status,
            actor,
            MembershipProgramme.Clip(reason, CommunityProgrammeLimits.MaxReasonLength),
            occurredAt);
}
