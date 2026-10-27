using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Domain.Rewards;

/// <summary>
/// One member's sign-up bonus.
/// <para>
/// <b>The backend awards the points; the client shows a state.</b> The register: "§19 says the
/// backend must award the points, so the client shows a <em>state</em>. The dangerous version is the
/// one where the client optimistically shows a credited balance."
/// </para>
/// <para>
/// <b><see cref="SignupBonusState.Blocked"/> carries a member-safe reason and nothing else.</b> The
/// register: "The <c>Blocked</c> copy is deliberately vague — no mention of the check that was
/// tripped. A fraud refusal that explains itself is a map for getting round it, and this is asserted
/// in <c>creator-economy.test.ts</c>." It is asserted on this side too — see the integration suite.
/// </para>
/// <para>
/// <b>Acknowledging is idempotent and does not itself award anything.</b> A second acknowledgement
/// returns the same state; whether the bonus is <em>awarded</em> depends on whether the outstanding
/// requirements are met, which the server checks on the acknowledgement rather than trusting the
/// client to have checked before calling.
/// </para>
/// </summary>
public sealed class SignupBonus : AggregateRoot<SignupBonusId>, IAuditableEntity
{
    private SignupBonus(
        SignupBonusId id,
        Guid userId,
        CreatorId creatorId,
        int points,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt) : base(id)
    {
        UserId = userId;
        CreatorId = creatorId;
        Points = points;
        State = SignupBonusState.Eligible;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
    }

    private SignupBonus() { }

    public Guid UserId { get; private set; }

    public CreatorId CreatorId { get; private set; } = default!;

    public SignupBonusState State { get; private set; }

    /// <summary>The points on offer. The server's number, and never the client's.</summary>
    public int Points { get; private set; }

    /// <summary>
    /// Why the member cannot have it, in words that are safe to show. Never names a risk check.
    /// </summary>
    public string? MemberSafeReason { get; private set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }

    public DateTimeOffset? AwardedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Uniquely indexed. Two acknowledgements carrying one key produce one award.</summary>
    public string? AcknowledgeIdempotencyKey { get; private set; }

    /// <summary>The reward reference the award published. The anti-double-award barrier at B15.</summary>
    public string? GrantReference { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static SignupBonus Offer(
        SignupBonusId id,
        Guid userId,
        CreatorId creatorId,
        int points,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt) =>
        new(id, userId, creatorId, points, createdAt, expiresAt);

    /// <summary>
    /// The member accepts, and the server decides what that is worth.
    /// </summary>
    /// <param name="requirementsMet">
    /// Whether every outstanding requirement is satisfied. Decided by the handler against real
    /// state, never taken from the request.
    /// </param>
    /// <param name="grantReference">
    /// This module's own handle for the award. Carried into the points grant so a redelivery awards
    /// once.
    /// </param>
    /// <returns>True when this call is the one that awarded, so the caller publishes exactly once.</returns>
    public bool Acknowledge(
        bool requirementsMet, string grantReference, string? idempotencyKey, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grantReference);

        if (State is SignupBonusState.Awarded
            or SignupBonusState.AlreadyClaimed
            or SignupBonusState.Blocked
            or SignupBonusState.NotEligible)
        {
            // Idempotent by construction: a bonus already decided stays decided, and a repeat
            // acknowledgement reads back the same state rather than moving anything.
            return false;
        }

        if (ExpiresAt is { } expiry && expiry <= now)
        {
            State = SignupBonusState.Expired;
            Touch(now);
            return false;
        }

        AcknowledgedAt ??= now;
        AcknowledgeIdempotencyKey ??= idempotencyKey;

        if (!requirementsMet)
        {
            State = SignupBonusState.Pending;
            Touch(now);
            return false;
        }

        State = SignupBonusState.Awarded;
        AwardedAt = now;
        GrantReference = grantReference;
        Touch(now);

        RaiseDomainEvent(new SignupBonusAwardedDomainEvent(Id, UserId, Points, now));

        if (Points > 0)
        {
            RaiseDomainEvent(new CreatorPointsEarnedDomainEvent(
                CreatorId, UserId, Points, "SignupBonus", grantReference, now,
                CreatorGrantPolicyVersions.SignupBonus(Points)));
        }

        return true;
    }

    /// <summary>
    /// A risk refusal.
    /// <para>
    /// <paramref name="memberSafeReason"/> is the only thing that reaches the member and must never
    /// name the signal. The handler logs the signal; this row does not carry it.
    /// </para>
    /// </summary>
    public void Block(string memberSafeReason, DateTimeOffset now)
    {
        if (State == SignupBonusState.Awarded)
        {
            return;
        }

        State = SignupBonusState.Blocked;
        MemberSafeReason = string.IsNullOrWhiteSpace(memberSafeReason)
            ? "This bonus is not available on your account."
            : memberSafeReason.Trim();
        Touch(now);
    }

    public void MarkNotEligible(string memberSafeReason, DateTimeOffset now)
    {
        if (State is SignupBonusState.Awarded or SignupBonusState.Blocked)
        {
            return;
        }

        State = SignupBonusState.NotEligible;
        MemberSafeReason = memberSafeReason.Trim();
        Touch(now);
    }

    public void Expire(DateTimeOffset now)
    {
        if (State is SignupBonusState.Awarded
            or SignupBonusState.Blocked
            or SignupBonusState.Expired)
        {
            return;
        }

        State = SignupBonusState.Expired;
        Touch(now);
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

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}
