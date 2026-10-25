using System.Reflection;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Points, and the fact that they are not money.
/// <para>
/// <b>The first test is the whole design.</b> A reward account has no currency, no exponent and no
/// <c>Money</c> anywhere on it — because a point is a count of something the platform chose to
/// recognise, and a type that carried a currency would be one line away from being summed with a
/// wallet balance.
/// </para>
/// </summary>
public class RewardAccountTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_reward_account_carries_no_money_and_no_currency()
    {
        var moneyish = typeof(RewardAccount)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property =>
                property.PropertyType == typeof(Money)
                || property.Name.Contains("Currency", StringComparison.Ordinal)
                || property.Name.Contains("MinorUnits", StringComparison.Ordinal))
            .Select(property => property.Name)
            .ToList();

        moneyish.Should().BeEmpty(
            "points are not money; a currency here would let somebody add them to a balance. "
            + "Offenders: " + string.Join(", ", moneyish));
    }

    /// <summary>
    /// <b>The anti-double-award barrier, checked in the aggregate as well as at the database.</b> An
    /// at-least-once bus redelivers; the in-aggregate check catches a redelivery inside one unit of
    /// work and the unique index catches one across two.
    /// </summary>
    [Fact]
    public void Two_grants_carrying_one_reference_award_points_once()
    {
        var account = Open();

        account.Grant(Guid.NewGuid(), 100, "ReferralQualified", "referral:abc", Now)
            .Should().BeTrue();

        account.Grant(Guid.NewGuid(), 100, "ReferralQualified", "referral:abc", Now)
            .Should().BeFalse("the grantor's reference is what makes an earning one earning");

        account.Balance.Should().Be(100);
        account.LifetimeEarned.Should().Be(100);
        account.Entries.Should().ContainSingle();
    }

    [Fact]
    public void Converting_reduces_the_balance_and_never_the_lifetime_total()
    {
        var account = Open();

        account.Grant(Guid.NewGuid(), 500, "GuidePublished", "guide:1", Now);
        account.Convert(Guid.NewGuid(), 200, "conversion:1", Now);

        account.Balance.Should().Be(300);
        account.LifetimeEarned.Should().Be(500, "what was earned never decreases");
        account.LifetimeConverted.Should().Be(200);
    }

    /// <summary>
    /// Refusing rather than clamping. A conversion a member cannot afford must fail loudly; the
    /// alternative is a balance that quietly becomes zero and credit backed by nothing.
    /// </summary>
    [Fact]
    public void Converting_more_points_than_the_account_holds_is_refused()
    {
        var account = Open();

        account.Grant(Guid.NewGuid(), 100, "GuidePublished", "guide:1", Now);

        var attempt = () => account.Convert(Guid.NewGuid(), 101, "conversion:1", Now);

        attempt.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.RewardInsufficientPoints);

        account.Balance.Should().Be(100, "a refused conversion moves nothing");
    }

    /// <summary>
    /// A block is a hold rather than a forfeiture: the points stay, and the refusal deliberately says
    /// nothing about which check it tripped.
    /// </summary>
    [Fact]
    public void A_blocked_account_keeps_its_points_and_cannot_convert()
    {
        var account = Open();

        account.Grant(Guid.NewGuid(), 500, "GuidePublished", "guide:1", Now);
        account.BlockConversion(Guid.NewGuid(), "Under review.", null, Now);

        var attempt = () => account.Convert(Guid.NewGuid(), 100, "conversion:1", Now);

        attempt.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.RewardNotEligible);

        account.Balance.Should().Be(500);

        account.AllowConversion(Guid.NewGuid(), "Review cleared.", null, Now).Should().BeTrue();
        account.Convert(Guid.NewGuid(), 100, "conversion:1", Now);

        account.Balance.Should().Be(400);
    }

    [Fact]
    public void A_grant_of_no_points_is_refused()
    {
        var account = Open();

        var attempt = () => account.Grant(Guid.NewGuid(), 0, "Bonus", "bonus:1", Now);

        attempt.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Conversions_are_countable_within_a_window_for_the_daily_cap()
    {
        var account = Open();

        account.Grant(Guid.NewGuid(), 1_000, "GuidePublished", "guide:1", Now.AddDays(-5));
        account.Convert(Guid.NewGuid(), 100, "conversion:1", Now.AddDays(-2));
        account.Convert(Guid.NewGuid(), 150, "conversion:2", Now);

        account.ConvertedSince(Now.AddDays(-1)).Should().Be(150);
        account.ConvertedSince(Now.AddDays(-7)).Should().Be(250);
    }

    // ── Vesting (D-9, RWD-07) ───────────────────────────────────────────────────────────────

    /// <summary>Q3: pending points are earned, and can be neither converted nor spent.</summary>
    [Fact]
    public void A_grant_that_has_not_vested_is_earned_but_not_spendable()
    {
        var account = Open();

        account.Grant(Guid.NewGuid(), 500, "GuidePublished", "guide:1", Now, vestsAt: Now.AddDays(7));

        account.Balance.Should().Be(0);
        account.PendingBalance.Should().Be(500);
        account.LifetimeEarned.Should().Be(500);
        account.NextVestsAt.Should().Be(Now.AddDays(7));

        var convert = () => account.Convert(Guid.NewGuid(), 500, "conversion:1", Now);
        convert.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.RewardInsufficientPoints);

        var spend = () => account.Spend(Guid.NewGuid(), 500, "Redemption", "redemption:1", Now);
        spend.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.RewardInsufficientPoints);
    }

    [Fact]
    public void Vesting_makes_only_due_grants_spendable_and_only_once()
    {
        var account = Open();

        account.Grant(Guid.NewGuid(), 500, "GuidePublished", "guide:1", Now, vestsAt: Now.AddDays(7));
        account.Grant(Guid.NewGuid(), 200, "GuidePublished", "guide:2", Now.AddDays(3), vestsAt: Now.AddDays(10));

        account.VestDue(Now.AddDays(6)).Should().Be(0, "nothing is due yet");

        account.VestDue(Now.AddDays(8)).Should().Be(500);
        account.VestDue(Now.AddDays(8)).Should().Be(0, "a grant vests once");

        account.Balance.Should().Be(500);
        account.PendingBalance.Should().Be(200);
        account.NextVestsAt.Should().Be(Now.AddDays(10));
        account.Entries.Single(entry => entry.Reference == "guide:1").VestedAt.Should().Be(Now.AddDays(8));

        account.VestDue(Now.AddDays(10)).Should().Be(200);

        account.Balance.Should().Be(700);
        account.PendingBalance.Should().Be(0);
        account.NextVestsAt.Should().BeNull();
    }

    /// <summary>
    /// D-5: points granted before vesting existed carry no vesting date and stay convertible, and a
    /// grant with no vesting date is spendable at once.
    /// </summary>
    [Fact]
    public void A_grant_with_no_vesting_date_counts_as_vested()
    {
        var account = Open();

        account.Grant(Guid.NewGuid(), 100, "Milestone", "milestone:first-guide", Now);

        account.Balance.Should().Be(100);
        account.PendingBalance.Should().Be(0);
        account.Entries.Single().IsPending.Should().BeFalse();
        account.VestDue(Now.AddYears(1)).Should().Be(0);
    }

    // ── Reversing pending points (D-9, RWD-08) ──────────────────────────────────────────────

    [Fact]
    public void A_pending_grant_is_taken_back_and_never_vests()
    {
        var account = Open();
        account.Grant(Guid.NewGuid(), 500, "GuidePublished", "guide:1", Now, vestsAt: Now.AddDays(7));

        account.ReversePending(Guid.NewGuid(), "guide:1", "Guide deleted.", Now.AddDays(1))
            .Should().Be(RewardReversalOutcome.Reversed);

        account.PendingBalance.Should().Be(0);
        account.Balance.Should().Be(0);
        account.LifetimeEarned.Should().Be(500, "what was earned is history");
        account.LifetimeReversed.Should().Be(500);
        account.NextVestsAt.Should().BeNull();

        var grant = account.Entries.Single(entry => entry.Reference == "guide:1");
        grant.ReversedAt.Should().Be(Now.AddDays(1));
        grant.ReversalReason.Should().Be("Guide deleted.");

        var reversal = account.Entries.Single(entry => entry.IsReversal);
        reversal.Points.Should().Be(500);
        reversal.IsDebit.Should().BeTrue();
        reversal.Reference.Should().Be($"reversal:{grant.EntryId:N}");

        account.VestDue(Now.AddDays(30)).Should().Be(0, "points taken back never vest");
    }

    /// <summary>
    /// D-9: points that could have been spent stay earned. The grantor cannot know whether its grant
    /// vested, so the request is recorded rather than refused.
    /// </summary>
    [Fact]
    public void A_vested_grant_stays_earned_and_the_request_is_recorded()
    {
        var account = Open();
        account.Grant(Guid.NewGuid(), 500, "GuidePublished", "guide:1", Now, vestsAt: Now.AddDays(7));
        account.VestDue(Now.AddDays(7));

        account.ReversePending(Guid.NewGuid(), "guide:1", "Guide deleted.", Now.AddDays(8))
            .Should().Be(RewardReversalOutcome.Declined);

        account.Balance.Should().Be(500);
        account.LifetimeReversed.Should().Be(0);
        account.Entries.Should().NotContain(entry => entry.IsReversal);

        var grant = account.Entries.Single();
        grant.ReversalDeclinedAt.Should().Be(Now.AddDays(8));
        grant.ReversalReason.Should().Be("Guide deleted.");
    }

    [Fact]
    public void A_grant_is_taken_back_once_however_often_it_is_asked()
    {
        var account = Open();
        account.Grant(Guid.NewGuid(), 500, "GuidePublished", "guide:1", Now, vestsAt: Now.AddDays(7));

        account.ReversePending(Guid.NewGuid(), "guide:1", "Guide deleted.", Now).Should().Be(RewardReversalOutcome.Reversed);
        account.ReversePending(Guid.NewGuid(), "guide:1", "Guide deleted.", Now).Should().Be(RewardReversalOutcome.AlreadyDecided);

        account.LifetimeReversed.Should().Be(500);
        account.Entries.Count(entry => entry.IsReversal).Should().Be(1);
        account.ReversePending(Guid.NewGuid(), "guide:2", "Never granted.", Now).Should().Be(RewardReversalOutcome.NotFound);
    }

    private static RewardAccount Open() =>
        RewardAccount.Open(RewardAccountId.New(), Guid.NewGuid(), Now);
}
