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

    private static RewardAccount Open() =>
        RewardAccount.Open(RewardAccountId.New(), Guid.NewGuid(), Now);
}
