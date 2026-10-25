using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Policies;

namespace Diaspora.Modules.Financial.Application.Rewards;

/// <summary>
/// When a new points grant becomes spendable (D-9, RWD-07).
/// <para>
/// <b>Pending for a week by default.</b> Points that can be reversed are points that have not been
/// spent: a guide deleted the day after it was published, or a signal staff are still looking at,
/// can take its points back only while they are still pending. After that they stay earned, and
/// money is never taken back automatically.
/// </para>
/// <para>
/// The window is a <c>vestingDays</c> parameter on the member's own <c>reward.conversion</c>
/// policy, resolved by the country on their financial account as the rate is. Absent means
/// <see cref="DefaultVestingDays"/>, because the seeder never rewrites a published policy; zero
/// means spendable at once.
/// </para>
/// </summary>
public sealed class RewardVesting
{
    public const int DefaultVestingDays = 7;

    public const int MaximumVestingDays = 365;

    private readonly FinancialLimitsResolver _limits;
    private readonly IFinancialAccountRepository _accounts;

    public RewardVesting(FinancialLimitsResolver limits, IFinancialAccountRepository accounts)
    {
        _limits = limits;
        _accounts = accounts;
    }

    /// <summary>When a grant made at <paramref name="grantedAt"/> vests, or null for spendable now.</summary>
    public async Task<DateTimeOffset?> VestsAtAsync(
        Guid memberUserId, DateTimeOffset grantedAt, CancellationToken cancellationToken = default)
    {
        var account = await _accounts.FindByOwnerAsync(
            memberUserId, FinancialAccountType.Personal, cancellationToken);

        var policy = await _limits.MostSpecificAsync(
            FinancialPolicyKeys.RewardConversion, account?.CountryCode, cancellationToken);

        var days = (int)Math.Clamp(
            policy?.Int64Parameter(FinancialPolicyParameters.VestingDays) ?? DefaultVestingDays,
            0,
            MaximumVestingDays);

        return days == 0 ? null : grantedAt.AddDays(days);
    }
}
