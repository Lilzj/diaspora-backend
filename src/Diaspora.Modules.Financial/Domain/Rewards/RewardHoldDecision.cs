using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Rewards;

/// <summary>
/// One decision to hold a points account from conversion, or to lift the hold. Owned by the account.
/// <para>
/// <b>Kept because nothing else keeps it.</b> The platform's audit service writes a log line, not a
/// record, and the account's <see cref="RewardAccount.BlockReason"/> holds only the hold in force and
/// is cleared when it lifts. "Who held this member, why, and who let them go" has to outlive both, so
/// every decision is appended here and none is ever changed.
/// </para>
/// <para>
/// <b>The reason is an operator's note and never reaches the member.</b> A held member is told only
/// that conversion is held; naming the check that tripped is a map for getting round it.
/// </para>
/// <para><see cref="DecisionId"/> is caller-supplied. <c>ValueGeneratedNever()</c>.</para>
/// </summary>
public sealed class RewardHoldDecision : ValueObject
{
    private RewardHoldDecision(
        Guid decisionId, bool isHeld, string reason, Guid? decidedByUserId, DateTimeOffset decidedAt)
    {
        DecisionId = decisionId;
        IsHeld = isHeld;
        Reason = reason;
        DecidedByUserId = decidedByUserId;
        DecidedAt = decidedAt;
    }

    private RewardHoldDecision() { }

    public Guid DecisionId { get; private set; }

    /// <summary>True for a hold placed, false for a hold lifted.</summary>
    public bool IsHeld { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>The staff member who decided. Null for a decision the platform made itself.</summary>
    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    internal static RewardHoldDecision Record(
        Guid decisionId, bool isHeld, string reason, Guid? decidedByUserId, DateTimeOffset decidedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new RewardHoldDecision(decisionId, isHeld, reason.Trim(), decidedByUserId, decidedAt);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return DecisionId;
        yield return IsHeld;
        yield return Reason;
        yield return DecidedByUserId;
        yield return DecidedAt;
    }
}
