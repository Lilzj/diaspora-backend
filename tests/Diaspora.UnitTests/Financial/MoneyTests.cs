using Diaspora.Modules.Financial.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// The type every figure on the platform is stored in.
/// <para>
/// The interesting tests here are the refusals. A money type that quietly produced a negative, a
/// wrong-currency sum or a rounding artefact would be wrong in a way nothing downstream could
/// detect.
/// </para>
/// </summary>
public class MoneyTests
{
    [Fact]
    public void Minor_units_are_what_is_stored_and_the_amount_is_derived()
    {
        var money = Money.FromMinorUnits(2500, "GBP");

        money.MinorUnits.Should().Be(2500);
        money.Amount.Should().Be(25.00m);
        money.Currency.Should().Be("GBP");
    }

    /// <summary>
    /// A platform assuming two decimal places would be wrong about a quarter of the world's
    /// currencies, silently, by a factor of a hundred.
    /// </summary>
    [Theory]
    [InlineData(0, 2500, 2500)]
    [InlineData(2, 2500, 25)]
    [InlineData(3, 2500, 2.5)]
    public void The_exponent_decides_what_the_minor_units_mean(
        int exponent, long minorUnits, decimal expected)
    {
        Money.FromMinorUnits(minorUnits, "JPY", exponent).Amount.Should().Be(expected);
    }

    [Fact]
    public void A_major_unit_figure_rounds_half_away_from_zero()
    {
        Money.FromAmount(25.005m, "GBP").MinorUnits.Should().Be(2501);
        Money.FromAmount(25.004m, "GBP").MinorUnits.Should().Be(2500);
    }

    [Fact]
    public void A_negative_amount_cannot_be_represented()
    {
        var negative = () => Money.FromMinorUnits(-1, "GBP");

        negative.Should().Throw<ArgumentOutOfRangeException>(
            "direction is carried by the ledger entry, not by the sign of the amount");
    }

    [Fact]
    public void An_unusable_currency_code_is_refused()
    {
        var invalid = () => Money.FromMinorUnits(100, "POUNDS");

        invalid.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void A_currency_code_is_normalised_to_upper_case()
    {
        Money.FromMinorUnits(100, "gbp").Currency.Should().Be("GBP");
    }

    /// <summary>The rule the whole platform rests on: two currencies never meet.</summary>
    [Fact]
    public void Two_currencies_cannot_be_added()
    {
        var add = () => Money.FromMinorUnits(100, "GBP").Add(Money.FromMinorUnits(100, "USD"));

        add.Should().Throw<InvalidOperationException>()
            .WithMessage("*never converts*");
    }

    [Fact]
    public void Two_currencies_cannot_be_compared()
    {
        var compare = () => Money.FromMinorUnits(100, "GBP").CompareTo(Money.FromMinorUnits(1, "USD"));

        compare.Should().Throw<InvalidOperationException>();
    }

    /// <summary>
    /// Clamping would turn "this wallet does not have enough" into "this wallet now has zero", which
    /// is a balance error that survives every test because it never fails loudly.
    /// </summary>
    [Fact]
    public void Subtracting_below_zero_throws_rather_than_clamping()
    {
        var subtract = () =>
            Money.FromMinorUnits(100, "GBP").Subtract(Money.FromMinorUnits(101, "GBP"));

        subtract.Should().Throw<InvalidOperationException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public void A_rate_is_applied_on_the_minor_unit_and_rounds_away_from_zero()
    {
        // 7.5% of 1333 minor units is 99.975 — which does not exist.
        Money.FromMinorUnits(1333, "GBP").MultiplyBy(0.075m).MinorUnits.Should().Be(100);
    }

    [Fact]
    public void Summing_an_empty_sequence_answers_null_rather_than_a_zero_in_an_invented_currency()
    {
        Money.Sum([]).Should().BeNull();
    }

    [Fact]
    public void Summing_amounts_in_one_currency_totals_them()
    {
        var total = Money.Sum(
        [
            Money.FromMinorUnits(100, "GBP"),
            Money.FromMinorUnits(250, "GBP")
        ]);

        total!.MinorUnits.Should().Be(350);
    }

    /// <summary>
    /// A cached static would be shared between two aggregates and EF would write nulls into the
    /// second — the trap the platform's <c>ValueObjectSharingArchitectureTests</c> exists to catch.
    /// </summary>
    [Fact]
    public void Zero_is_a_fresh_instance_every_time()
    {
        Money.Zero("GBP").Should().NotBeSameAs(Money.Zero("GBP"));
    }

    [Fact]
    public void Two_equal_amounts_are_equal_by_value()
    {
        Money.FromMinorUnits(100, "GBP").Should().Be(Money.FromMinorUnits(100, "GBP"));
    }
}
