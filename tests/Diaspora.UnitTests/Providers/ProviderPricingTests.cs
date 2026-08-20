using Diaspora.Modules.Providers.Domain.Enums;
using Diaspora.Modules.Providers.Domain.Errors;
using Diaspora.Modules.Providers.Domain.ValueObjects;
using Diaspora.Modules.Providers.Extensibility.Pricing;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Providers;

/// <summary>
/// Pricing, and the formatting members actually read.
/// <para>
/// The client has one pricing shape and validates nothing, so every case here is something it
/// currently renders wrongly: "£0 — £0" for a free service, "£12 — £8" for an inverted range,
/// and nothing at all for a quoted one.
/// </para>
/// </summary>
public class ProviderPricingTests
{
    private static readonly DefaultPricingFormatter Formatter = new();

    [Fact]
    public void A_range_needs_both_amounts_and_a_currency()
    {
        var pricing = ProviderPricing.Create(PricingModel.Range, 8m, 12m, "GBP");

        pricing.IsSuccess.Should().BeTrue();
        Formatter.Format(pricing.Value).Should().Be("£8 – £12");
    }

    [Fact]
    public void An_inverted_range_is_refused()
    {
        var pricing = ProviderPricing.Create(PricingModel.Range, 12m, 8m, "GBP");

        pricing.IsFailure.Should().BeTrue();
        pricing.Error.Should().Be(ProviderErrors.PricingRangeInverted);
    }

    [Fact]
    public void A_quoted_price_may_carry_no_amounts()
    {
        ProviderPricing.Create(PricingModel.Quote, 40m, currency: "GBP")
            .Error.Should().Be(ProviderErrors.PricingModelMismatch);

        var quoted = ProviderPricing.Create(PricingModel.Quote);

        quoted.IsSuccess.Should().BeTrue();
        Formatter.Format(quoted.Value).Should().Be("Price on request");
    }

    [Fact]
    public void A_free_service_renders_as_free_rather_than_zero()
    {
        var free = ProviderPricing.Create(PricingModel.Free);

        free.IsSuccess.Should().BeTrue();

        // The client's single shape renders this as "£0 — £0", which reads as a mistake.
        Formatter.Format(free.Value).Should().Be("Free");
    }

    [Fact]
    public void An_hourly_rate_is_distinguishable_from_a_fixed_price()
    {
        var hourly = ProviderPricing.Create(PricingModel.Hourly, 40m, currency: "GBP");
        var fixedPrice = ProviderPricing.Create(PricingModel.Fixed, 40m, currency: "GBP");

        Formatter.Format(hourly.Value).Should().Be("£40/hr");
        Formatter.Format(fixedPrice.Value).Should().Be("£40");
    }

    [Fact]
    public void A_single_amount_model_refuses_a_maximum()
    {
        // "£40 per hour" and "£40 to £60" are different offers.
        var confused = ProviderPricing.Create(PricingModel.Hourly, 40m, 60m, "GBP");

        confused.IsFailure.Should().BeTrue();
        confused.Error.Should().Be(ProviderErrors.PricingModelMismatch);
    }

    [Fact]
    public void A_published_amount_needs_a_valid_currency_code()
    {
        ProviderPricing.Create(PricingModel.Fixed, 40m)
            .Error.Should().Be(ProviderErrors.PricingCurrencyInvalid);

        ProviderPricing.Create(PricingModel.Fixed, 40m, currency: "POUNDS")
            .Error.Should().Be(ProviderErrors.PricingCurrencyInvalid);
    }

    [Fact]
    public void A_negative_amount_is_refused()
    {
        ProviderPricing.Create(PricingModel.Fixed, -5m, currency: "GBP")
            .Error.Should().Be(ProviderErrors.PricingNegative);
    }

    [Fact]
    public void An_ambiguous_currency_renders_as_its_iso_code()
    {
        // "$" means at least a dozen currencies on a platform serving the Americas, Africa and
        // the Pacific. A Canadian member shown "$40" by a US provider has been told something
        // false.
        var mauritian = ProviderPricing.Create(PricingModel.Fixed, 400m, currency: "MUR");

        Formatter.Format(mauritian.Value).Should().Be("MUR 400");
    }

    [Fact]
    public void A_negotiable_price_reads_as_a_starting_point()
    {
        var negotiable = ProviderPricing.Create(
            PricingModel.Fixed, 60m, currency: "GBP", isNegotiable: true);

        Formatter.Format(negotiable.Value).Should().Be("From £60");
    }

    [Fact]
    public void A_whole_amount_loses_its_decimals_and_a_fractional_one_keeps_them()
    {
        var whole = ProviderPricing.Create(PricingModel.Fixed, 8m, currency: "GBP");
        var fractional = ProviderPricing.Create(PricingModel.Fixed, 8.50m, currency: "GBP");

        // A menu of "£8.00" reads like a receipt, not a price list.
        Formatter.Format(whole.Value).Should().Be("£8");
        Formatter.Format(fractional.Value).Should().Be("£8.50");
    }

    [Fact]
    public void An_unspecified_price_renders_as_nothing_at_all()
    {
        Formatter.Format(ProviderPricing.Unspecified).Should().BeNull();
    }
}
