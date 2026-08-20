using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Extensibility.Rewards;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Financial.Application.Rewards;

/// <summary>
/// B15's own implementation of the points seam.
/// <para>
/// <b>Inside the Financial Platform, and that is the whole design.</b> A consumer outside B15 takes
/// a dependency on <see cref="IRewardPointsAccount"/> and on nothing of B15's domain; the balance,
/// the entries and the rules that govern them stay here. See the interface for why the operation is
/// synchronous rather than an event.
/// </para>
/// <para>
/// <b>Stages, and never saves.</b> The caller's unit of work commits the debit alongside whatever it
/// paid for, so a redemption that failed to write cannot leave points missing — and DSP4003 stays
/// able to see that the caller saves.
/// </para>
/// </summary>
public sealed class RewardPointsAccount : IRewardPointsAccount
{
    private readonly IRewardAccountRepository _accounts;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public RewardPointsAccount(
        IRewardAccountRepository accounts, IGuidGenerator guids, IClock clock)
    {
        _accounts = accounts;
        _guids = guids;
        _clock = clock;
    }

    public async Task<RewardPointsBalance> GetBalanceAsync(
        Guid memberUserId, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.FindByMemberAsync(memberUserId, cancellationToken);

        return account is null
            ? RewardPointsBalance.Empty
            : new RewardPointsBalance(
                account.Balance, account.LifetimeEarned, account.IsConversionBlocked);
    }

    public async Task<RewardPointsSpendResult> TrySpendAsync(
        Guid memberUserId,
        long points,
        string source,
        string spendReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        ArgumentException.ThrowIfNullOrWhiteSpace(spendReference);

        var account = await _accounts.FindByMemberAsync(memberUserId, cancellationToken);

        // No account row means no points. A member who has never earned anything cannot redeem, and
        // opening an account to refuse them would leave a row nothing ever reads.
        if (account is null)
        {
            return RewardPointsSpendResult.InsufficientPoints;
        }

        if (account.IsConversionBlocked)
        {
            return RewardPointsSpendResult.Blocked;
        }

        if (account.Balance < points)
        {
            return RewardPointsSpendResult.InsufficientPoints;
        }

        var debited = account.Spend(
            _guids.NewSequentialGuid(), points, source, spendReference, _clock.UtcNow);

        if (!debited)
        {
            return RewardPointsSpendResult.AlreadySpent;
        }

        _accounts.Update(account);

        return RewardPointsSpendResult.Debited;
    }

    public async Task<bool> RefundAsync(
        Guid memberUserId,
        long points,
        string source,
        string refundReference,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(points);
        ArgumentException.ThrowIfNullOrWhiteSpace(refundReference);

        var account = await _accounts.FindByMemberAsync(memberUserId, cancellationToken);

        if (account is null)
        {
            return false;
        }

        var refunded = account.RefundSpend(
            _guids.NewSequentialGuid(), points, source, refundReference, _clock.UtcNow);

        if (!refunded)
        {
            return false;
        }

        _accounts.Update(account);

        return true;
    }
}
