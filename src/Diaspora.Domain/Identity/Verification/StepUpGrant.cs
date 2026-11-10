using Diaspora.Domain.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Domain.Identity.Verification;

/// <summary>
/// A record that one session re-proved possession of the member's phone, and what that proof
/// authorises until it expires.
/// <para>
/// <b>The session is the load-bearing field.</b> A grant bound only to the member would be
/// defeated by the attack step-up exists to stop: an attacker holding a stolen refresh token waits
/// for the member to approve their own withdrawal and spends the window. Bound to the session that
/// answered the challenge, the attacker's token carries a different <c>sid</c> and holds nothing,
/// whatever the member does. Revocation comes free — a revoked session's token is rejected before
/// the pipeline is reached, so a grant cannot outlive the session that earned it.
/// </para>
/// <para>
/// <b>Append-only.</b> Nothing mutates a grant. A member who steps up twice has two rows, the
/// later one simply reaching further into the future, and the gate asks whether any row is still
/// live. That makes this table a security ledger as well as a mechanism: "when did this member
/// last prove themselves, from which session, for what" is one query, which is the question a
/// payment provider's onboarding review asks and the question an incident starts with.
/// </para>
/// <para>
/// <b>Nothing sweeps the expired rows, and that is consistent rather than overlooked.</b> No sweep
/// exists for spent <see cref="OtpChallenge"/> rows either, which are the same class of record and
/// far more numerous — one per code sent, against one per step-up completed here. If a retention
/// policy is ever written for one it should cover both, and it should keep rather than delete:
/// "when did this member last prove themselves" is the first question of a payment dispute, and a
/// table that forgets cannot answer it.
/// </para>
/// <para>
/// <b>It names the challenge that proved it</b> rather than restating the proof. The code, its
/// hash, its attempt budget and the number it was sent to all live on <see cref="OtpChallenge"/>
/// already; duplicating any of that would create a second place for the truth to live.
/// </para>
/// </summary>
public sealed class StepUpGrant : AggregateRoot<StepUpGrantId>
{
    public UserId UserId { get; private set; } = default!;

    /// <summary>The session that answered the challenge. Only this session holds the grant.</summary>
    public SessionId SessionId { get; private set; } = default!;

    /// <summary>One of <c>StepUpScopes</c>. Stored as the string so the catalogue can grow.</summary>
    public string Scope { get; private set; } = default!;

    /// <summary>The challenge whose correct answer earned this grant.</summary>
    public OtpChallengeId ChallengeId { get; private set; } = default!;

    public DateTimeOffset GrantedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    private StepUpGrant() { }

    private StepUpGrant(
        StepUpGrantId id,
        UserId userId,
        SessionId sessionId,
        string scope,
        OtpChallengeId challengeId,
        DateTimeOffset grantedAt,
        DateTimeOffset expiresAt) : base(id)
    {
        UserId = userId;
        SessionId = sessionId;
        Scope = scope;
        ChallengeId = challengeId;
        GrantedAt = grantedAt;
        ExpiresAt = expiresAt;
    }

    public static StepUpGrant Issue(
        UserId userId,
        SessionId sessionId,
        string scope,
        OtpChallengeId challengeId,
        DateTimeOffset now,
        TimeSpan lifetime)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(sessionId);
        ArgumentNullException.ThrowIfNull(challengeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);

        if (lifetime <= TimeSpan.Zero)
        {
            // A zero or negative lifetime would mint a grant that is already dead, and the member
            // would answer a code and be refused anyway with nothing to tell them why. Loud here
            // rather than mysterious at the gate.
            throw new DomainException(
                "A step-up grant must last longer than no time at all.",
                "Identity.StepUp.LifetimeInvalid");
        }

        return new StepUpGrant(
            StepUpGrantId.New(),
            userId,
            sessionId,
            scope.Trim(),
            challengeId,
            now,
            now.Add(lifetime));
    }

    public bool IsLiveAt(DateTimeOffset now) => ExpiresAt > now;
}
