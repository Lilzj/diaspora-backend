using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Catalog;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// The catalogue that makes every premium feature data rather than code.
/// </summary>
public class MonetizationCatalogTests
{
    [Fact]
    public void A_product_starts_as_a_draft_and_announces_itself()
    {
        var product = Product();

        product.Status.Should().Be(MonetizationProductStatus.Draft);
        product.DomainEvents.Should().ContainSingle();
    }

    /// <summary>
    /// A product with neither cannot be bought and cannot say why, which is the worst of both.
    /// </summary>
    [Fact]
    public void A_product_needs_either_a_price_or_a_pricing_policy()
    {
        var product = Product();

        var setNothing = () => product.SetPrice(null, null, FinancialTestData.Now);

        setNothing.Should().Throw<DomainException>().WithMessage("*either a price or a pricing policy*");
    }

    [Fact]
    public void A_product_with_no_price_cannot_be_activated()
    {
        var product = Product();

        var activate = () => product.Activate(FinancialTestData.Now);

        activate.Should().Throw<DomainException>().WithMessage("*cannot be activated*");
    }

    [Fact]
    public void A_priced_product_activates_and_is_on_sale()
    {
        var product = Priced();

        product.IsOnSale(FinancialTestData.Now).Should().BeTrue();
        product.Price!.MinorUnits.Should().Be(499);
    }

    [Fact]
    public void An_availability_window_decides_whether_a_product_is_on_sale()
    {
        var product = Priced();

        product.SetAvailability(
            FinancialTestData.Now.AddDays(1), FinancialTestData.Now.AddDays(7), FinancialTestData.Now);

        product.IsOnSale(FinancialTestData.Now).Should().BeFalse();
        product.IsOnSale(FinancialTestData.Now.AddDays(2)).Should().BeTrue();
        product.IsOnSale(FinancialTestData.Now.AddDays(8)).Should().BeFalse();
    }

    [Fact]
    public void A_duration_outside_the_platform_bounds_is_refused()
    {
        var product = Priced();

        var setGrant = () => product.SetGrant(
            PlacementKind.Boost, 9999, 1.5m, true, FinancialTestData.Now);

        setGrant.Should().Throw<DomainException>().WithMessage("*between 1 and*");
    }

    [Fact]
    public void A_priority_weight_is_clamped_to_something_a_ranker_can_survive()
    {
        var product = Priced();

        product.SetGrant(PlacementKind.Boost, 7, 500m, true, FinancialTestData.Now);

        product.PriorityWeight.Should().Be(10m);
    }

    /// <summary>Retiring a product must never revoke what somebody already paid for.</summary>
    [Fact]
    public void A_system_product_cannot_be_retired()
    {
        var product = Priced(isSystem: true);

        var retire = () => product.Retire(FinancialTestData.Now);

        retire.Should().Throw<DomainException>().WithMessage("*platform product*");
    }

    [Fact]
    public void An_eligibility_rule_of_a_kind_replaces_the_previous_one()
    {
        var product = Priced();

        product.AddEligibility(Guid.NewGuid(), EligibilityRuleKind.CountryAllowList, "GB");
        product.AddEligibility(Guid.NewGuid(), EligibilityRuleKind.CountryAllowList, "GB,NG");

        product.Eligibility.Should().ContainSingle();
        product.Rule(EligibilityRuleKind.CountryAllowList)!.ListContains("NG").Should().BeTrue();
    }

    private static MonetizationProduct Product() =>
        MonetizationProduct.Define(
            MonetizationProductId.New(),
            "marketplace.listing.boost.7d",
            "Boost listing for 7 days",
            "Increase search priority and local reach.",
            MonetizationProductCategory.BoostedListing,
            MonetizationSubjectType.MarketplaceListing,
            FinancialTestData.Now,
            isSystem: false);

    private static MonetizationProduct Priced(bool isSystem = false)
    {
        var product = MonetizationProduct.Define(
            MonetizationProductId.New(),
            "marketplace.listing.boost.7d",
            "Boost listing for 7 days",
            "Increase search priority and local reach.",
            MonetizationProductCategory.BoostedListing,
            MonetizationSubjectType.MarketplaceListing,
            FinancialTestData.Now,
            isSystem);

        product.SetPrice(FinancialTestData.Gbp_(4.99m), null, FinancialTestData.Now);
        product.SetGrant(PlacementKind.Boost, 7, 1.5m, true, FinancialTestData.Now);
        product.Activate(FinancialTestData.Now);

        return product;
    }
}
