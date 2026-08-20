using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Domain.Referrals;

/// <summary>
/// One creator's referral identity — the code they share.
/// <para>
/// <b>One active code per creator, enforced by a unique index on the creator as well as on the
/// code.</b> §12 of the phase brief: "The same creator must not receive multiple active referral
/// identities unless explicitly supported." Issuing is idempotent on the creator: a second call
/// returns the first code rather than minting another, which is also what makes
/// <c>POST /referrals/code</c> safe to retry.
/// </para>
/// </summary>
public sealed class CreatorReferralCode : AggregateRoot<CreatorReferralCodeId>, IAuditableEntity
{
    private CreatorReferralCode(
        CreatorReferralCodeId id,
        CreatorId creatorId,
        Guid userId,
        string code,
        DateTimeOffset issuedAt) : base(id)
    {
        CreatorId = creatorId;
        UserId = userId;
        Code = code;
        IssuedAt = issuedAt;
        CreatedAt = issuedAt;
    }

    private CreatorReferralCode() { }

    public CreatorId CreatorId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    /// <summary>Upper-case, from an unambiguous alphabet, uniquely indexed.</summary>
    public string Code { get; private set; } = string.Empty;

    public bool IsActive { get; private set; } = true;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorReferralCode Issue(
        CreatorReferralCodeId id,
        CreatorId creatorId,
        Guid userId,
        string code,
        DateTimeOffset issuedAt) =>
        new(id, creatorId, userId, code.Trim().ToUpperInvariant(), issuedAt);

    public void Deactivate() => IsActive = false;

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
/// One person referred by one creator.
/// <para>
/// <b>Four states before a reward, and keeping them apart is the whole of CREATOR-GAP-009.</b> The
/// register: "§18 is explicit that not every signup qualifies — so a client counting signups and
/// promising a reward would be promising money the platform never agreed to pay."
/// <see cref="ReferralState.Invited"/>, <see cref="ReferralState.SignedUp"/>,
/// <see cref="ReferralState.Qualified"/> and <see cref="ReferralState.Rewarded"/> are four
/// different facts and this row never collapses two of them.
/// </para>
/// <para>
/// <b>Qualification is decided by <c>ReferralQualificationPolicy</c> and never by a client.</b> What
/// this aggregate does is record the decision and refuse to make it twice: a referral that has
/// already qualified cannot qualify again, which is what stops a redelivered event paying twice.
/// </para>
/// <para>
/// <b>The refusal a member sees never names the check that refused it.</b> A rejected referral
/// carries a member-safe <see cref="StateReason"/>; the anti-abuse detail is in the audit log and
/// nowhere on the wire.
/// </para>
/// </summary>
public sealed class CreatorReferral : AggregateRoot<CreatorReferralId>, IAuditableEntity
{
    private CreatorReferral(
        CreatorReferralId id,
        CreatorReferralCodeId codeId,
        CreatorId referrerCreatorId,
        Guid referrerUserId,
        DateTimeOffset invitedAt) : base(id)
    {
        ReferralCodeId = codeId;
        ReferrerCreatorId = referrerCreatorId;
        ReferrerUserId = referrerUserId;
        State = ReferralState.Invited;
        InvitedAt = invitedAt;
        ExpiresAt = invitedAt.AddDays(CreatorLimits.ReferralQualificationDays);
        CreatedAt = invitedAt;
    }

    private CreatorReferral() { }

    public CreatorReferralCodeId ReferralCodeId { get; private set; } = default!;

    public CreatorId ReferrerCreatorId { get; private set; } = default!;

    public Guid ReferrerUserId { get; private set; }

    /// <summary>Set when somebody registers with the code. Uniquely indexed — one referral per invitee.</summary>
    public Guid? InviteeUserId { get; private set; }

    /// <summary>
    /// A display name the server chose to reveal — often a first name only. Never an email, never a
    /// phone number, and never the invitee's identifier.
    /// </summary>
    public string InviteeLabel { get; private set; } = "Someone";

    public ReferralState State { get; private set; }

    /// <summary>Why it has not qualified yet, in words a member may read.</summary>
    public string? StateReason { get; private set; }

    public DateTimeOffset InvitedAt { get; private set; }

    public DateTimeOffset? SignedUpAt { get; private set; }

    public DateTimeOffset? QualifiedAt { get; private set; }

    public DateTimeOffset? RewardedAt { get; private set; }

    /// <summary>When the window closes. A referral that has not qualified by then expires.</summary>
    public DateTimeOffset ExpiresAt { get; private set; }

    public int RewardPoints { get; private set; }

    /// <summary>The reward reference the qualification published. The anti-double-award barrier.</summary>
    public string? RewardGrantReference { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorReferral Invite(
        CreatorReferralId id,
        CreatorReferralCode code,
        DateTimeOffset invitedAt)
    {
        ArgumentNullException.ThrowIfNull(code);

        return new CreatorReferral(id, code.Id, code.CreatorId, code.UserId, invitedAt);
    }

    /// <summary>
    /// Somebody registered with the code.
    /// <para>
    /// <b>That is all this means, and the state says so.</b> No reward is owed, nothing is
    /// published, and the member's dashboard shows a signup rather than a qualified referral.
    /// </para>
    /// </summary>
    public Result RecordSignup(Guid inviteeUserId, string inviteeLabel, DateTimeOffset now)
    {
        if (inviteeUserId == ReferrerUserId)
        {
            return Result.Failure(CreatorErrors.ReferralSelf);
        }

        if (State != ReferralState.Invited)
        {
            return Result.Failure(CreatorErrors.ReferralDuplicate);
        }

        InviteeUserId = inviteeUserId;
        InviteeLabel = string.IsNullOrWhiteSpace(inviteeLabel) ? "Someone" : inviteeLabel.Trim();
        State = ReferralState.SignedUp;
        SignedUpAt = now;
        Touch(now);

        return Result.Success();
    }

    /// <summary>The invitee has started working toward the rule, and the member can see that.</summary>
    public void MarkPending(string? reason, DateTimeOffset now)
    {
        if (State != ReferralState.SignedUp)
        {
            return;
        }

        State = ReferralState.Pending;
        StateReason = Trim(reason);
        Touch(now);
    }

    /// <summary>
    /// The qualification rule is satisfied.
    /// <para>
    /// Refused from anywhere but <see cref="ReferralState.SignedUp"/> or
    /// <see cref="ReferralState.Pending"/>, which is what makes a redelivered qualification a no-op
    /// rather than a second reward. §12: "repeated qualification events".
    /// </para>
    /// </summary>
    public Result Qualify(int rewardPoints, string grantReference, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grantReference);

        if (State is not (ReferralState.SignedUp or ReferralState.Pending))
        {
            return Result.Failure(CreatorErrors.ReferralNotEligible);
        }

        if (now > ExpiresAt)
        {
            return Result.Failure(CreatorErrors.ReferralNotEligible);
        }

        State = ReferralState.Qualified;
        QualifiedAt = now;
        StateReason = null;
        RewardPoints = Math.Max(0, rewardPoints);
        RewardGrantReference = grantReference;
        Touch(now);

        RaiseDomainEvent(new ReferralQualifiedDomainEvent(
            Id,
            ReferrerCreatorId,
            ReferrerUserId,
            InviteeUserId ?? Guid.Empty,
            RewardPoints,
            now));

        return Result.Success();
    }

    /// <summary>The reward has been granted. Recorded so the dashboard can separate owed from paid.</summary>
    public void MarkRewarded(DateTimeOffset now)
    {
        if (State != ReferralState.Qualified)
        {
            return;
        }

        State = ReferralState.Rewarded;
        RewardedAt = now;
        Touch(now);
    }

    /// <summary>
    /// Refused.
    /// <para>
    /// <b><paramref name="memberSafeReason"/> is the only thing that reaches the member.</b> The
    /// anti-abuse signal that produced the refusal is logged and never stored on this row: a refusal
    /// that explains itself is a map for getting round it.
    /// </para>
    /// </summary>
    public void Reject(string memberSafeReason, DateTimeOffset now)
    {
        if (State is ReferralState.Rewarded or ReferralState.Rejected)
        {
            return;
        }

        State = ReferralState.Rejected;
        StateReason = Trim(memberSafeReason);
        Touch(now);
    }

    /// <summary>The window closed without qualification.</summary>
    public void Expire(DateTimeOffset now)
    {
        if (State is ReferralState.Qualified
            or ReferralState.Rewarded
            or ReferralState.Rejected
            or ReferralState.Expired)
        {
            return;
        }

        State = ReferralState.Expired;
        Touch(now);
    }

    public bool HasLapsed(DateTimeOffset now) =>
        State is ReferralState.Invited or ReferralState.SignedUp or ReferralState.Pending
        && now > ExpiresAt;

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

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
