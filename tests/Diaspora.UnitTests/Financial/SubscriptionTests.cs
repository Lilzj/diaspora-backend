using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Subscriptions;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Subscriptions, and the fairness rules that are easiest to get wrong.
/// </summary>
public class SubscriptionTests
{
    [Fact]
    public void A_plan_grants_entitlements_and_a_subscription_copies_them()
    {
        var subscription = Subscribe();

        subscription.EntitlementKeys.Should().Contain("analytics.advanced");
        subscription.GrantsAccess.Should().BeTrue();
    }

    /// <summary>
    /// A plan repriced or re-scoped next month must not silently change what somebody is already
    /// paying for — the same reasoning that puts a policy version on a ledger entry.
    /// </summary>
    [Fact]
    public void Changing_the_plan_afterwards_does_not_change_a_live_subscription()
    {
        var plan = Plan();
        var subscription = Subscribe(plan);

        plan.Grant(Guid.NewGuid(), "something.new");

        subscription.EntitlementKeys.Should().NotContain("something.new");
    }

    [Fact]
    public void A_trial_starts_in_the_trial_state_and_ends_when_the_trial_does()
    {
        var subscription = Subscribe(withTrial: true);

        subscription.Status.Should().Be(SubscriptionStatus.Trial);
        subscription.TrialEndsAt.Should().Be(FinancialTestData.Now.AddDays(14));
    }

    /// <summary>
    /// Ending access the instant somebody clicks cancel is taking back something that was paid for.
    /// </summary>
    [Fact]
    public void Cancelling_keeps_access_until_the_end_of_the_paid_period()
    {
        var subscription = Subscribe();

        subscription.CancelAtEndOfPeriod("Too expensive.", FinancialTestData.Now);

        subscription.CancelAtPeriodEnd.Should().BeTrue();
        subscription.GrantsAccess.Should().BeTrue();
        subscription.Status.Should().Be(SubscriptionStatus.Active);
    }

    [Fact]
    public void A_scheduled_cancellation_can_be_undone()
    {
        var subscription = Subscribe();

        subscription.CancelAtEndOfPeriod(null, FinancialTestData.Now);
        subscription.ResumeAutoRenewal(FinancialTestData.Now);

        subscription.CancelAtPeriodEnd.Should().BeFalse();
    }

    /// <summary>
    /// A card that expired on a Friday should not remove somebody's paid features before anybody
    /// could have told them.
    /// </summary>
    [Fact]
    public void A_failed_renewal_keeps_access_for_the_grace_window()
    {
        var subscription = Subscribe();

        subscription.EnterGracePeriod(FinancialTestData.Now, graceDays: 14);

        subscription.Status.Should().Be(SubscriptionStatus.GracePeriod);
        subscription.GrantsAccess.Should().BeTrue();
        subscription.GraceEndsAt.Should().Be(FinancialTestData.Now.AddDays(14));
    }

    [Fact]
    public void A_renewal_clears_the_grace_period_and_extends_the_window()
    {
        var plan = Plan();
        var subscription = Subscribe(plan);

        subscription.EnterGracePeriod(FinancialTestData.Now);
        subscription.Renew(plan, FinancialTestData.Now, () => Guid.NewGuid());

        subscription.Status.Should().Be(SubscriptionStatus.Active);
        subscription.GraceEndsAt.Should().BeNull();
        subscription.RenewalCount.Should().Be(1);
    }

    [Fact]
    public void A_renewal_announces_itself_as_a_renewal()
    {
        var plan = Plan();
        var subscription = Subscribe(plan);

        subscription.ClearDomainEvents();
        subscription.Renew(plan, FinancialTestData.Now, () => Guid.NewGuid());

        subscription.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Ending_a_subscription_withdraws_access_and_announces_it()
    {
        var subscription = Subscribe();

        subscription.ClearDomainEvents();
        subscription.End(SubscriptionStatus.Expired, "The grace period ran out.", FinancialTestData.Now);

        subscription.GrantsAccess.Should().BeFalse();
        subscription.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void A_cancelled_subscription_cannot_be_cancelled_again()
    {
        var subscription = Subscribe();

        subscription.End(SubscriptionStatus.Cancelled, null, FinancialTestData.Now);

        var cancel = () => subscription.CancelAtEndOfPeriod(null, FinancialTestData.Now);

        cancel.Should().Throw<DomainException>();
    }

    [Fact]
    public void A_subscription_whose_period_has_ended_is_due_for_renewal()
    {
        var subscription = Subscribe();

        subscription.IsDueForRenewal(FinancialTestData.Now).Should().BeFalse();
        subscription.IsDueForRenewal(FinancialTestData.Now.AddMonths(2)).Should().BeTrue();
    }

    private static SubscriptionPlan Plan()
    {
        var plan = SubscriptionPlan.Define(
            SubscriptionPlanId.New(),
            "premium.monthly",
            "Premium",
            "More of everything.",
            SubscriptionTier.Premium,
            BillingInterval.Monthly,
            FinancialTestData.Gbp_(7.99m),
            FinancialTestData.Now,
            trialDays: 14);

        plan.Grant(Guid.NewGuid(), "listings.concurrent", "25");
        plan.Grant(Guid.NewGuid(), "analytics.advanced");

        return plan;
    }

    private static Subscription Subscribe(SubscriptionPlan? plan = null, bool withTrial = false) =>
        Subscription.Start(
            SubscriptionId.New(),
            Guid.NewGuid(),
            FinancialAccountId.New(),
            plan ?? Plan(),
            FinancialTestData.Now,
            () => Guid.NewGuid(),
            withTrial);
}
