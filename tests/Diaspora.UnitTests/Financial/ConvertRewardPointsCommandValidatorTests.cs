using Diaspora.Modules.Financial.Application.Rewards;
using Diaspora.Modules.Financial.Domain.Policies;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// What a conversion request must carry before anything prices it.
/// <para>
/// Points are deliberately not here: the pricer refuses them in the member's terms, which
/// <see cref="RewardConversionQuoteTests"/> pins. These hold the line between the two.
/// </para>
/// </summary>
public sealed class ConvertRewardPointsCommandValidatorTests
{
    private static readonly ConvertRewardPointsCommandValidator Validator = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_conversion_needs_a_key(string? key)
    {
        Validator.Validate(Command() with { IdempotencyKey = key }).IsValid.Should().BeFalse();
    }

    [Fact]
    public void A_key_may_be_as_long_as_a_reference_and_no_longer()
    {
        var longest = new string('k', FinancialLimits.MaxReferenceLength);

        Validator.Validate(Command() with { IdempotencyKey = longest }).IsValid.Should().BeTrue();
        Validator.Validate(Command() with { IdempotencyKey = longest + "k" }).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("US")]
    [InlineData("USDX")]
    public void A_currency_is_three_letters(string currency)
    {
        Validator.Validate(Command() with { Currency = currency }).IsValid.Should().BeFalse();
    }

    /// <summary>The currency is optional: the rate says which one it is in.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("MUR")]
    public void A_currency_may_be_left_out(string? currency)
    {
        Validator.Validate(Command() with { Currency = currency }).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Points_are_left_to_the_pricer(long points)
    {
        Validator.Validate(Command() with { Points = points }).IsValid.Should().BeTrue();
    }

    private static ConvertRewardPointsCommand Command() =>
        new() { Points = 500, IdempotencyKey = "conversion-key" };
}
