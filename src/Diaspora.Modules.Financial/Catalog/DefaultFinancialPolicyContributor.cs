using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Extensibility.Catalog;
using Diaspora.Modules.Financial.Infrastructure.Pricing;

namespace Diaspora.Modules.Financial.Catalog;

/// <summary>
/// The policies the platform ships, so that nothing is hard-coded from the first request.
/// <para>
/// <b>Every number in this file is a starting value an operator is expected to change.</b> That is
/// the whole point of the policy engine: a commission rate lives on a row with an effective date and
/// an audit trail, not in a constant somebody has to redeploy to move. The values here are
/// deliberately conservative, and the platform-fee and refund-window policies are marked as system
/// policies because they are guarantees rather than settings.
/// </para>
/// <para>
/// <b>Seeding creates and never overwrites.</b> A rate a deployment has retuned is left exactly as
/// it is on the next restart — the alternative would make the whole measure-and-adjust loop
/// pointless, and the person who changed it at three in the morning would find it back where it
/// started.
/// </para>
/// </summary>
public sealed class DefaultFinancialPolicyContributor : IFinancialPolicyContributor
{
    public string Module => FinancialModuleMetadata.Name;

    public IEnumerable<FinancialPolicySeed> GetPolicies()
    {
        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.MarketplaceCommission,
            Kind = FinancialPolicyKind.MarketplaceCommission,
            Description = "What the platform takes from a completed marketplace sale.",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PercentageCommissionRule.PercentageParameter] = "5.0",

                // A floor and a ceiling, because a percentage alone is wrong at both ends: 5% of a
                // two-pound item rounds to nothing, and 5% of a car is a number nobody intended.
                [PercentageCommissionRule.MinimumParameter] = "50",
                [PercentageCommissionRule.MaximumParameter] = "50000"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.HousingCommission,
            Kind = FinancialPolicyKind.HousingCommission,
            Description = "What the platform takes from a housing placement.",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PercentageCommissionRule.PercentageParameter] = "3.0",
                [PercentageCommissionRule.MaximumParameter] = "100000"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.ProviderCommission,
            Kind = FinancialPolicyKind.ProviderCommission,
            Description = "What the platform takes from a provider engagement.",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PercentageCommissionRule.PercentageParameter] = "7.5",
                [PercentageCommissionRule.MaximumParameter] = "50000"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.OrganizationCommission,
            Kind = FinancialPolicyKind.OrganizationCommission,
            Description = "What the platform takes from an organization's paid activity.",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PercentageCommissionRule.PercentageParameter] = "2.5"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.PlatformFee,
            Kind = FinancialPolicyKind.PlatformFee,
            Description = "The processing fee applied to a monetization purchase.",
            IsSystem = true,
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PercentageCommissionRule.PercentageParameter] = "0.0"
            }
        };

        // Giving is free. Stated as a policy rather than as an absence, so that a deployment which
        // has to recover its processing costs can set a figure without a release — and so that a
        // deployment which does is doing it deliberately, on a row with an audit trail.
        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.DonationFee,
            Kind = FinancialPolicyKind.DonationRule,
            Description = "What the platform keeps from a donation. Zero by default, and by intent.",
            IsSystem = true,
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FlatFeeRule.FeeParameter] = "0"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.ContributionFee,
            Kind = FinancialPolicyKind.ContributionRule,
            Description = "What the platform keeps from a community contribution. Zero by default.",
            IsSystem = true,
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FlatFeeRule.FeeParameter] = "0"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.RefundWindow,
            Kind = FinancialPolicyKind.RefundRule,
            Description =
                "How long after buying a placement it may be cancelled with a full refund.",
            IsSystem = true,
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["window_hours"] = "24",

                // A placement that has already run is not refundable however recently it was bought;
                // otherwise a member buys a boost, gets the traffic and cancels inside the window.
                ["requires_unstarted"] = "true"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.RevenueDistribution,
            Kind = FinancialPolicyKind.RevenueDistribution,
            Description =
                "How a sale divides between the platform and the seller. Recorded, never paid out.",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [PercentageCommissionRule.PercentageParameter] = "5.0"
            }
        };

        // ── B15.1 ────────────────────────────────────────────────────────────────────────────
        //
        // Four rules the platform now has surfaces for, seeded so a deployment starts with sane
        // bounds rather than none. The capability switches in FinancialOptions decide whether the
        // surfaces are open at all; these decide what they permit when they are.

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.DepositLimits,
            Kind = FinancialPolicyKind.DepositRule,
            Description = "What a member may add to their own balance, and how much in a day.",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // £1 minimum, £2 000 a time, £5 000 a day. Deliberately conservative: a deployment
                // that means to hold larger balances publishes a version saying so, which is a
                // decision somebody makes rather than a default they inherit.
                [FinancialPolicyParameters.MinimumMinorUnits] = "100",
                [FinancialPolicyParameters.MaximumMinorUnits] = "200000",
                [FinancialPolicyParameters.DailyMaximumMinorUnits] = "500000"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.WithdrawalLimits,
            Kind = FinancialPolicyKind.WithdrawalRule,
            Description = "What a member may take out, how much in a day, and what it costs them.",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FinancialPolicyParameters.MinimumMinorUnits] = "500",
                [FinancialPolicyParameters.MaximumMinorUnits] = "200000",
                [FinancialPolicyParameters.DailyMaximumMinorUnits] = "500000",

                // No fee by default. A deployment that charges one publishes a version; inheriting a
                // charge on somebody's own money is not a default anybody should acquire silently.
                [FinancialPolicyParameters.FeeMinorUnits] = "0"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.SubscriptionProration,
            Kind = FinancialPolicyKind.SubscriptionProration,
            Description =
                "How a mid-period plan change is priced, and how much warning a lapse gets.",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // An upgrade charges the difference now; a downgrade waits for the boundary, because
                // somebody who has paid for the month keeps the month.
                [FinancialPolicyParameters.ProrateUpgrade] = "true",
                [FinancialPolicyParameters.DeferDowngrade] = "true",
                [FinancialPolicyParameters.ExpiryLeadDays] = "3"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.RewardConversion,
            Kind = FinancialPolicyKind.RewardConversion,
            Description = "What a reward point is worth, and how many may be converted.",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Zero, deliberately. A rate seeded above zero would turn conversion on by seeding
                // data — the platform would owe money because somebody deployed. An operator
                // publishes a version with a real rate when the product decision has been taken, and
                // Financial:EnableRewardConversion has to be on as well.
                [FinancialPolicyParameters.MinorUnitsPerPoint] = "0",
                [FinancialPolicyParameters.MinimumPoints] = "500",
                [FinancialPolicyParameters.MaximumPoints] = "50000",
                [FinancialPolicyParameters.DailyMaximumPoints] = "50000"
            }
        };

        yield return new FinancialPolicySeed
        {
            PolicyKey = FinancialPolicyKeys.RewardGrant,
            Kind = FinancialPolicyKind.RewardGrant,
            Description = "How many points staff may award by hand: per grant, and per staff member per day.",
            Parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Four referrals' worth in one grant, and five such grants a day for one staff member.
                // Enough to put a member's missed earning right; not enough for one account to mint a
                // balance worth converting at scale.
                [FinancialPolicyParameters.MaximumPoints] = "1000",
                [FinancialPolicyParameters.DailyMaximumPoints] = "5000"
            }
        };
    }
}
