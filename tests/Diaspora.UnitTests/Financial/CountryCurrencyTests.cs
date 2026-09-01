using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Catalog;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// A member's wallet is denominated in the currency of the country they live in.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this prevents.</b> <c>UserRegisteredConsumer</c> passes the member's
/// <c>CountryOfResidence</c> and a null currency. <c>FinancialIdentityService</c> stored the
/// country on the account and then resolved the currency as
/// <c>NormalizeCurrency(currency) ?? DefaultCurrency</c> — falling back to <c>"GBP"</c> without
/// ever reading the country it had been handed one line earlier.
/// </para>
/// <para>
/// The effect was that <b>every member of the platform was opened in Pound Sterling</b>. Observed
/// on a release build: the Wallet screen read "£0.00 / GBP" for a member resident in Mauritius
/// whose own marketplace listings were priced "Rs 11500" (UAT-124). Both figures were correct —
/// the listing takes its currency from the listing, the wallet from a constant.
/// </para>
/// </remarks>
public sealed class CountryCurrencyTests
{
    [Theory]
    [InlineData("MU", "MUR")]
    [InlineData("GB", "GBP")]
    [InlineData("NG", "NGN")]
    [InlineData("GH", "GHS")]
    [InlineData("KE", "KES")]
    [InlineData("ZA", "ZAR")]
    public void Every_served_country_maps_to_its_own_currency(string country, string expected)
    {
        // These six are the platform's supported and featured countries. If any of
        // them resolved to sterling, the defect is back.
        CountryCurrency.For(country).Should().Be(expected);
    }

    [Fact]
    public void A_mauritian_member_is_not_opened_in_sterling()
    {
        // The specific case that reached a release build.
        CountryCurrency.For("MU").Should().NotBe("GBP");
    }

    [Theory]
    [InlineData("mu")]
    [InlineData(" MU ")]
    public void The_country_code_is_read_case_and_whitespace_insensitively(string country)
    {
        // It arrives from an integration event, not from a validated request.
        CountryCurrency.For(country).Should().Be("MUR");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ZZ")]
    public void An_unserved_country_returns_null_rather_than_guessing(string? country)
    {
        // Null means "fall back to the configured default", which is the honest
        // answer for a market the platform has not opened. Returning a guess would
        // reintroduce the defect with a different wrong currency.
        CountryCurrency.For(country).Should().BeNull();
    }

    [Fact]
    public void Every_currency_this_map_can_produce_is_one_the_platform_activates()
    {
        // Mapping a country to a currency the deployment has not seeded would open a
        // wallet in a currency nothing can price — worse than the wrong-but-working
        // currency this replaced. The catalogue contributor is the seed.
        var activated = new DefaultCurrencyContributor()
            .GetCurrencies()
            .Select(currency => currency.Code)
            .ToHashSet(StringComparer.Ordinal);

        CountryCurrency.KnownCurrencies.Should().OnlyContain(code => activated.Contains(code));
    }

    [Fact]
    public void The_map_covers_every_country_the_platform_serves()
    {
        // Mirrors `location.countries` where `IsSupported` or `IsFeatured` is true.
        // Location owns that fact; this is the subset Financial needs, and this
        // assertion is what stops the duplicate drifting silently.
        var served = new[] { "GB", "GH", "KE", "MU", "NG", "ZA" };

        CountryCurrency.KnownCountries.Should().Contain(served);
    }
}
