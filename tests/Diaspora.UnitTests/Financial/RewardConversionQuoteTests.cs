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

    private static RewardConversionTerms Terms(decimal rate, long minimum = 1) => new()
    {
        IsEnabled = true,
        Currency = "USD",
        Exponent = 2,
        MinorUnitsPerPoint = rate,
        MinimumPoints = minimum
    };
}
