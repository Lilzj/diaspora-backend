namespace Diaspora.Modules.Financial.Extensibility.Rewards;

/// <summary>
/// What a module outside the Financial Platform may do with a member's reward points.
/// <para>
/// <b>Added in Wave 6 so the Creator Platform could redeem without owning a balance.</b> B15's
/// <c>RewardAccount</c> is the platform's single points ledger and its own documentation names "a
/// creator platform" as one of the grantors the mechanism exists for. Earning was already covered by
/// a published contract — <c>RewardPointsGrantedIntegrationEvent</c>, which B15 consumes — but
/// spending was not, and a redemption that could not debit would either have been free or would have
/// needed a second balance somewhere else. This is the seam that made neither necessary.
/// </para>
/// <para>
/// <b>Synchronous, and that is the point.</b> Checking a balance and then publishing a "points
/// spent" event would leave a window in which two concurrent redemptions both saw enough points.
/// Reading and debiting through one interface, inside the caller's unit of work, makes the check and
/// the debit one decision — and B15's own unique index on the spend reference decides the race if
/// two requests get that far.
/// </para>
/// <para>
/// <b>Points are not money and nothing here says otherwise.</b> No currency, no amount, no exponent,
/// no wallet. The one operation that turns points into currency remains
/// <c>RewardAccount.Convert</c>, which lives inside B15 and has no seam.
/// </para>
/// </summary>
public interface IRewardPointsAccount
{
    /// <summary>
    /// How many points a member holds and whether they may spend them.
    /// <para>
    /// A member who has never earned anything has no account row, which reads as a zero balance
    /// rather than an absence — the same answer B15's own balance route gives.
    /// </para>
    /// </summary>
    Task<RewardPointsBalance> GetBalanceAsync(
        Guid memberUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Debits points for something that is not money.
    /// <para>
    /// Stages the write; the caller saves, so the debit and whatever it paid for commit together or
    /// not at all.
    /// </para>
    /// </summary>
    /// <param name="spendReference">
    /// The spender's own handle for the thing being bought. Uniquely indexed per account, so a
    /// retry debits once.
    /// </param>
    Task<RewardPointsSpendResult> TrySpendAsync(
        Guid memberUserId,
        long points,
        string source,
        string spendReference,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts spent points back, because the platform could not deliver what they bought.
    /// <para>
    /// Appends a compensating entry rather than editing the spend — B15's ledger rule, applied to
    /// points.
    /// </para>
    /// </summary>
    Task<bool> RefundAsync(
        Guid memberUserId,
        long points,
        string source,
        string refundReference,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A member's points, and whether they are available to spend.
/// <para>
/// <see cref="IsBlocked"/> carries no reason, deliberately. A hold on an account is a risk decision
/// and the member-facing sentence is the caller's to choose; B15 saying which check tripped would
/// put that sentence in two places, one of which is not the one shown.
/// </para>
/// </summary>
public sealed record RewardPointsBalance(long Available, long LifetimeEarned, bool IsBlocked)
{
    /// <summary>What a member with no account row has.</summary>
    public static readonly RewardPointsBalance Empty = new(0, 0, false);
}

/// <summary>What a spend attempt did.</summary>
public enum RewardPointsSpendResult
{
    /// <summary>The points were debited. The caller saves.</summary>
    Debited = 0,

    /// <summary>This reference has already been spent. A retry, and not an error.</summary>
    AlreadySpent = 1,

    /// <summary>The account does not hold that many points.</summary>
    InsufficientPoints = 2,

    /// <summary>The account is on hold. The caller decides what the member is told.</summary>
    Blocked = 3
}
