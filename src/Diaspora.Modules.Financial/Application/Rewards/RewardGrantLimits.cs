using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Rewards;

/// <summary>
/// What the <c>reward.grant</c> policy allows one staff member to grant (RWD-10).
/// <para>
/// Shared by the grant route, the grant request and its approval, so the three cannot disagree about
/// a limit. <b>The per-grant and daily limits fail closed:</b> this policy exists only to bound the
/// route, so one missing either refuses every grant. The approval threshold does not: a policy
/// published before it existed reads <see cref="DefaultApprovalThresholdPoints"/>, as an absent
/// <c>holdDays</c> reads its default.
/// </para>
/// </summary>
public sealed class RewardGrantLimits
{
    /// <summary>The largest grant one Admin may make alone when the policy does not say.</summary>
    public const long DefaultApprovalThresholdPoints = 500;

    private readonly FinancialLimitsResolver _limits;
    private readonly IRewardAccountRepository _accounts;
    private readonly IClock _clock;

    public RewardGrantLimits(
        FinancialLimitsResolver limits, IRewardAccountRepository accounts, IClock clock)
    {
        _limits = limits;
        _accounts = accounts;
        _clock = clock;
    }

    /// <summary>
    /// Whether <paramref name="grantorUserId"/> may grant <paramref name="points"/> now, measured
    /// against the per-grant maximum and their rolling day. Says nothing about the threshold;
    /// <see cref="RewardGrantVerdict.NeedsApproval"/> does.
    /// <para>
    /// Two grants racing from one staff member can both pass the daily check. That bounds one
    /// operator's hand, not a ledger invariant; the second approver is the stronger control.
    /// </para>
    /// </summary>
    public async Task<RewardGrantVerdict> CheckAsync(
        Guid grantorUserId, long points, CancellationToken cancellationToken = default)
    {
        var policy = await _limits.MostSpecificAsync(
            FinancialPolicyKeys.RewardGrant, countryCode: null, cancellationToken);

        var maximum = policy?.Int64Parameter(FinancialPolicyParameters.MaximumPoints);
        var dailyMaximum = policy?.Int64Parameter(FinancialPolicyParameters.DailyMaximumPoints);
        var threshold = policy?.Int64Parameter(FinancialPolicyParameters.ApprovalThresholdPoints)
            ?? DefaultApprovalThresholdPoints;

        if (maximum is not > 0 || dailyMaximum is not > 0)
        {
            return new RewardGrantVerdict(policy, threshold, Error.Conflict(
                FinancialErrorCodes.RewardGrantLimitsUnset,
                "Staff grants are off until a reward.grant policy states a per-grant and a daily maximum."));
        }

        if (points > maximum)
        {
            return new RewardGrantVerdict(policy, threshold, Error.Conflict(
                FinancialErrorCodes.RewardGrantAboveMaximum,
                $"The largest single grant is {maximum} points."));
        }

        var grantedToday = await _accounts.PointsGrantedBySinceAsync(
            grantorUserId, _clock.UtcNow.AddDays(-1), cancellationToken);

        return grantedToday + points > dailyMaximum
            ? new RewardGrantVerdict(policy, threshold, Error.Conflict(
                FinancialErrorCodes.RewardGrantDailyLimitReached,
                $"That would take you past the {dailyMaximum} points one person may grant in a day. "
                + $"You have granted {grantedToday} since this time yesterday."))
            : new RewardGrantVerdict(policy, threshold, Refusal: null);
    }

    /// <summary>Nobody grants, asks for or approves points for their own account (Q4).</summary>
    public static Error? RefuseSelf(Guid staffUserId, Guid memberUserId) =>
        staffUserId == memberUserId
            ? Error.Forbidden(
                FinancialErrorCodes.RewardGrantToSelf,
                "Staff cannot grant points to their own account; another Admin has to.")
            : null;
}

/// <summary>What the <c>reward.grant</c> policy says about one grant.</summary>
/// <param name="Policy">The version that decided it. Null only when none is published.</param>
/// <param name="ApprovalThreshold">The largest grant one Admin may make alone.</param>
/// <param name="Refusal">Why it is refused, or null.</param>
public sealed record RewardGrantVerdict(FinancialPolicy? Policy, long ApprovalThreshold, Error? Refusal)
{
    /// <summary>Whether a grant of this size needs a second Admin.</summary>
    public bool NeedsApproval(long points) => points > ApprovalThreshold;
}
