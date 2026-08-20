using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Promotions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Coupons, and the refusals that make them usable.
/// <para>
/// A coupon that silently does nothing is worse than one that is refused, so every refusal here has
/// its own code and its own message.
/// </para>
/// </summary>
public class PromotionTests
{
    [Fact]
    public void A_percentage_discount_takes_a_proportion_off()
    {
        var discount = Discount.OfPercentage(10m);

        discount.AmountOff(FinancialTestData.Gbp_(50m)).MinorUnits.Should().Be(500);
    }

    /// <summary>
    /// Ten percent off an unbounded purchase with no ceiling is an open cheque.
    /// </summary>
    [Fact]
    public void A_percentage_discount_respects_its_cap()
    {
        var discount = Discount.OfPercentage(50m, maximumMinorUnits: 500);

        discount.AmountOff(FinancialTestData.Gbp_(100m)).MinorUnits.Should().Be(500);
    }

    [Fact]
    public void A_discount_never_exceeds_the_price()
    {
        Discount.Fixed(FinancialTestData.Gbp_(100m))
            .AmountOff(FinancialTestData.Gbp_(25m))
            .MinorUnits.Should().Be(2500);
    }

    /// <summary>The platform's rule everywhere money meets money.</summary>
    [Fact]
    public void A_fixed_discount_in_another_currency_applies_nothing_rather_than_converting()
    {
        var discount = Discount.Fixed(Money.FromAmount(10m, "USD"));

        discount.AmountOff(FinancialTestData.Gbp_(50m)).IsZero.Should().BeTrue();
    }

    [Fact]
    public void A_free_discount_takes_the_whole_price()
    {
        Discount.Free().AmountOff(FinancialTestData.Gbp_(37.50m)).MinorUnits.Should().Be(3750);
    }

    [Fact]
    public void An_active_promotion_inside_its_window_applies()
    {
        var promotion = Promotion_();

        promotion.Evaluate(FinancialTestData.Gbp_(50m), "any.product", "GB", 0, FinancialTestData.Now)
            .Should().BeNull();
    }

    [Fact]
    public void A_draft_promotion_is_refused_as_not_applicable()
    {
        var promotion = Promotion_(activate: false);

        promotion.Evaluate(FinancialTestData.Gbp_(50m), null, null, 0, FinancialTestData.Now)
            .Should().Be(FinancialErrorCodes.PromotionNotApplicable);
    }

    [Fact]
    public void A_promotion_outside_its_window_reports_expiry()
    {
        var promotion = Promotion_();

        promotion.Evaluate(
                FinancialTestData.Gbp_(50m), null, null, 0, FinancialTestData.Now.AddDays(31))
            .Should().Be(FinancialErrorCodes.PromotionExpired);
    }

    [Fact]
    public void A_promotion_at_its_total_limit_reports_exhaustion()
    {
        var promotion = Promotion_();

        promotion.SetLimits(1, null, FinancialTestData.Now);
        promotion.RecordRedemption(FinancialTestData.Now);

        promotion.Status.Should().Be(PromotionStatus.Exhausted);
        promotion.Evaluate(FinancialTestData.Gbp_(50m), null, null, 0, FinancialTestData.Now)
            .Should().Be(FinancialErrorCodes.PromotionExhausted);
    }

    [Fact]
    public void A_member_at_their_own_limit_is_told_they_have_already_used_it()
    {
        var promotion = Promotion_();

        promotion.SetLimits(null, 1, FinancialTestData.Now);

        promotion.Evaluate(FinancialTestData.Gbp_(50m), null, null, 1, FinancialTestData.Now)
            .Should().Be(FinancialErrorCodes.PromotionAlreadyRedeemed);
    }

    [Fact]
    public void A_country_restricted_promotion_says_so()
    {
        var promotion = Promotion_();

        promotion.SetScope(["GB"], null, null, FinancialTestData.Now);

        promotion.Evaluate(FinancialTestData.Gbp_(50m), null, "NG", 0, FinancialTestData.Now)
            .Should().Be(FinancialErrorCodes.PromotionCountryRestricted);

        promotion.Evaluate(FinancialTestData.Gbp_(50m), null, "GB", 0, FinancialTestData.Now)
            .Should().BeNull();
    }

    [Fact]
    public void A_product_restricted_promotion_refuses_another_product()
    {
        var promotion = Promotion_();

        promotion.SetScope(null, ["credit.bundle.25"], null, FinancialTestData.Now);

        promotion.Evaluate(
                FinancialTestData.Gbp_(50m), "marketplace.listing.boost.7d", null, 0,
                FinancialTestData.Now)
            .Should().Be(FinancialErrorCodes.PromotionNotApplicable);
    }

    [Fact]
    public void A_minimum_spend_refuses_a_smaller_purchase()
    {
        var promotion = Promotion_();

        promotion.SetScope(null, null, FinancialTestData.Gbp_(20m), FinancialTestData.Now);

        promotion.Evaluate(FinancialTestData.Gbp_(10m), null, null, 0, FinancialTestData.Now)
            .Should().Be(FinancialErrorCodes.PromotionNotApplicable);

        promotion.Evaluate(FinancialTestData.Gbp_(20m), null, null, 0, FinancialTestData.Now)
            .Should().BeNull();
    }

    /// <summary>The maintained counter's only way down.</summary>
    [Fact]
    public void Reconciliation_can_correct_the_counter()
    {
        var promotion = Promotion_();

        promotion.RecordRedemption(FinancialTestData.Now);
        promotion.RecordRedemption(FinancialTestData.Now);
        promotion.ReconcileRedemptionCount(1, FinancialTestData.Now);

        promotion.RedeemedCount.Should().Be(1);
    }

    private static Promotion Promotion_(bool activate = true)
    {
        var promotion = Promotion.Define(
            PromotionId.New(),
            "welcome10",
            "Ten percent off",
            PromotionKind.Coupon,
            Discount.OfPercentage(10m, 10_000),
            DateWindow.ForDays(FinancialTestData.Now, 30),
            FinancialTestData.Now);

        if (activate)
        {
            promotion.Activate(FinancialTestData.Now);
        }

        return promotion;
    }
}
