using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Application.Rewards;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// A conversion takes only the points that buy whole minor units.
/// <para>
/// The credit is floored, and every requested point used to be debited anyway: at 0.01 minor units a
/// point, 550 points paid 5 and took 550. These pin the replacement — the fewest points that buy the
/// same credit — by example and by property.
/// </para>
/// </summary>
public sealed class RewardConversionQuoteTests
{
    [Theory]
    [InlineData(550, "0.01", 500, 5)]
    [InlineData(10_000, "0.01", 10_000, 100)]
    [InlineData(1_000, "1", 1_000, 1_000)]
    [InlineData(7, "3", 7, 21)]
    [InlineData(3, "2.5", 3, 7)]
    [InlineData(101, "0.5", 100, 50)]
    [InlineData(10, "0.333333", 10, 3)]
    [InlineData(19_999, "0.0001", 10_000, 1)]
    public void Only_the_points_that_buy_whole_minor_units_are_consumed(
        long requested, string rate, long consumed, long creditMinorUnits)
    {
        var quote = RewardConversionPricer.Quote(Terms(decimal.Parse(rate, System.Globalization.CultureInfo.InvariantCulture)), requested, balance: 1_000_000);

        quote.IsPermitted.Should().BeTrue(quote.RefusalMessage);
        quote.Points.Should().Be(requested);
        quote.PointsConsumed.Should().Be(consumed);
        quote.Credit.MinorUnits.Should().Be(creditMinorUnits);
    }

    [Fact]
    public void Points_worth_less_than_one_minor_unit_are_refused_rather_than_taken()
    {
        var quote = RewardConversionPricer.Quote(Terms(0.01m), points: 99, balance: 1_000);

        quote.IsPermitted.Should().BeFalse();
        quote.RefusalCode.Should().Be(FinancialErrorCodes.RewardBelowMinimum);
        quote.PointsConsumed.Should().Be(0);
    }

    /// <summary>
    /// The validator leaves points to the pricer, so nothing else stands between a request for no
    /// points, or fewer than none, and the balance. It threw on a negative number: the check that a
    /// conversion never takes more than was asked read 0 as more than -1, and the member got a 500.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(long.MinValue)]
    public void No_points_or_fewer_than_none_are_refused_in_the_members_terms(long points)
    {
        var quote = RewardConversionPricer.Quote(Terms(1m), points, balance: 1_000);

        quote.IsPermitted.Should().BeFalse();
        quote.RefusalCode.Should().Be(FinancialErrorCodes.RewardBelowMinimum);
        quote.RefusalMessage.Should().Be("Convert at least one point.");
        quote.PointsConsumed.Should().Be(0);
    }

    /// <summary>The limits apply to what would actually leave the balance.</summary>
    [Fact]
    public void The_minimum_is_measured_on_the_points_consumed()
    {
        // 499 points buy 4 minor units, which takes 400 — below a 500 minimum.
        var below = RewardConversionPricer.Quote(Terms(0.01m, minimum: 500), points: 499, balance: 1_000);
        below.IsPermitted.Should().BeFalse();
        below.RefusalCode.Should().Be(FinancialErrorCodes.RewardBelowMinimum);

        // 520 buy 5, which takes 500 — exactly the minimum.
        var at = RewardConversionPricer.Quote(Terms(0.01m, minimum: 500), points: 520, balance: 1_000);
        at.IsPermitted.Should().BeTrue(at.RefusalMessage);
        at.PointsConsumed.Should().Be(500);
    }

    /// <summary>"More than you hold" reads the number the member typed, not the smaller one consumed.</summary>
    [Fact]
    public void Asking_for_more_than_the_balance_is_refused_even_if_fewer_would_be_consumed()
    {
        var quote = RewardConversionPricer.Quote(Terms(0.01m), points: 550, balance: 520);

        quote.IsPermitted.Should().BeFalse();
        quote.RefusalCode.Should().Be(FinancialErrorCodes.RewardInsufficientPoints);
    }

    /// <summary>
    /// The property, over rates with up to six decimal places (the column's scale) and a wide range
    /// of point counts: never more than asked, exactly the same credit, and the fewest points that buy it.
    /// </summary>
    [Fact]
    public void Consumed_points_are_never_more_than_asked_and_buy_exactly_the_credit()
    {
        var random = new Random(20261022);

        for (var trial = 0; trial < 20_000; trial++)
        {
            var rate = random.Next(1, 1_000_000_000) / 1_000_000m;
            var requested = (long)random.Next(1, 2_000_000);

            var quote = RewardConversionPricer.Quote(Terms(rate), requested, balance: long.MaxValue);
            var credit = quote.Credit.MinorUnits;

            quote.PointsConsumed.Should().BeLessThanOrEqualTo(requested, $"rate {rate}, {requested} points");

            if (credit == 0)
            {
                quote.PointsConsumed.Should().Be(0);
                continue;
            }

            Math.Floor(quote.PointsConsumed * rate).Should().Be(credit, $"rate {rate}, {requested} points");
            Math.Floor((quote.PointsConsumed - 1) * rate).Should().BeLessThan(
                credit, $"one point fewer must buy less — rate {rate}, {requested} points");
        }
    }

    /// <summary>An account that may not move money out converts nothing, whatever else is true.</summary>
    [Fact]
    public void An_ineligible_account_is_refused_before_any_limit_is_read()
    {
        var terms = Terms(1m) with
        {
            AccountRefusalCode = FinancialErrorCodes.AccountNotEligible,
            AccountRefusalMessage = "Verify your identity."
        };

        var quote = RewardConversionPricer.Quote(terms, points: 500, balance: 1_000);

        quote.IsPermitted.Should().BeFalse();
        quote.RefusalCode.Should().Be(FinancialErrorCodes.AccountNotEligible);
        quote.RefusalMessage.Should().Be("Verify your identity.");
    }

    /// <summary>A deployment that does not convert says so first; the account is a later question.</summary>
    [Fact]
    public void The_deployment_switch_is_answered_before_the_account()
    {
        var terms = Terms(1m) with
        {
            IsEnabled = false,
            DisabledCode = FinancialErrorCodes.RewardConversionNotSupported,
            AccountRefusalCode = FinancialErrorCodes.AccountRestricted
        };

        RewardConversionPricer.Quote(terms, points: 500, balance: 1_000)
            .RefusalCode.Should().Be(FinancialErrorCodes.RewardConversionNotSupported);
    }

    /// <summary>
    /// A hold is refused as a hold, and says no more: the hold's own reason is an operator's note,
    /// and a refusal that names the check it tripped is a map for getting round it.
    /// </summary>
    [Fact]
    public void A_held_points_account_is_refused_without_the_holds_reason()
    {
        var quote = RewardConversionPricer.Quote(Terms(1m), points: 500, balance: 1_000, isHeld: true);

        quote.IsPermitted.Should().BeFalse();
        quote.RefusalCode.Should().Be(FinancialErrorCodes.RewardNotEligible);
        quote.RefusalMessage.Should().Be(RewardConversionPricer.HeldMessage);
    }

    // ── Standing: whether a member may convert before they type a number ────────────────────

    [Fact]
    public void A_member_below_the_minimum_is_told_the_minimum_and_their_balance()
    {
        var standing = RewardConversionPricer.Standing(Terms(1m, minimum: 500), balance: 320, isHeld: false);

        standing.Should().BeEquivalentTo(new RewardConversionStanding
        {
            CanConvert = false,
            ReasonCode = FinancialErrorCodes.RewardBelowMinimum,
            ReasonMessage = "You need at least 500 points to convert. You have 320.",
            MaximumNow = null
        });
    }

    /// <summary>The order is the order a member can act on: deployment, account, hold, balance, today.</summary>
    [Fact]
    public void The_deployment_then_the_account_then_a_hold_come_before_the_balance()
    {
        var terms = Terms(1m, minimum: 500);

        RewardConversionPricer.Standing(
                terms with { IsEnabled = false, DisabledCode = FinancialErrorCodes.RewardConversionNotSupported },
                balance: 0, isHeld: true)
            .ReasonCode.Should().Be(FinancialErrorCodes.RewardConversionNotSupported);

        RewardConversionPricer.Standing(
                terms with { AccountRefusalCode = FinancialErrorCodes.AccountNotEligible },
                balance: 0, isHeld: true)
            .ReasonCode.Should().Be(FinancialErrorCodes.AccountNotEligible);

        var held = RewardConversionPricer.Standing(terms, balance: 0, isHeld: true);
        held.ReasonCode.Should().Be(FinancialErrorCodes.RewardNotEligible);
        held.ReasonMessage.Should().Be(RewardConversionPricer.HeldMessage);
    }

    [Fact]
    public void Less_left_today_than_the_minimum_is_the_daily_limit()
    {
        var standing = RewardConversionPricer.Standing(
            Terms(1m, minimum: 100) with { DailyMaximumPoints = 1_000, DailyRemainingPoints = 40 },
            balance: 5_000,
            isHeld: false);

        standing.CanConvert.Should().BeFalse();
        standing.ReasonCode.Should().Be(FinancialErrorCodes.RewardDailyLimitReached);
    }

    [Theory]
    [InlineData(null, null, 700L, 700L)]
    [InlineData(300L, null, 1_000L, 300L)]
    [InlineData(2_000L, 800L, 5_000L, 800L)]
    [InlineData(2_000L, 800L, 500L, 500L)]
    public void The_most_a_member_can_convert_now_is_the_smallest_limit(
        long? maximum, long? dailyRemaining, long balance, long expected)
    {
        var terms = Terms(1m, minimum: 10) with
        {
            MaximumPoints = maximum,
            DailyMaximumPoints = dailyRemaining is null ? null : 10_000,
            DailyRemainingPoints = dailyRemaining
        };

        var standing = RewardConversionPricer.Standing(terms, balance, isHeld: false);

        standing.CanConvert.Should().BeTrue();
        standing.MaximumNow.Should().Be(expected);
    }

    private static RewardConversionTerms Terms(decimal rate, long minimum = 1) => new()
    {
        IsEnabled = true,
        Currency = "USD",
        Exponent = 2,
        MinorUnitsPerPoint = rate,
        MinimumPoints = minimum
    };
}
