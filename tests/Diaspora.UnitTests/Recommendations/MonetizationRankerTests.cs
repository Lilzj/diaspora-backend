using Diaspora.Contracts.Financial.V1.Monetization;
using Diaspora.Modules.Recommendations.Application.Monetization;
using Diaspora.Modules.Recommendations.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Recommendations;

/// <summary>
/// MON-GAP-011: what the monetization surface suggests, and the sentence it gives for each.
/// <para>
/// <b>The two rules that matter most are negative:</b> never suggest what the member already holds,
/// and never give a reason the member could check and find false.
/// </para>
/// </summary>
public class MonetizationRankerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Sofa = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Flat = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Clinic = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void An_unpromoted_listing_gets_its_cheapest_eligible_promotion_with_a_true_reason()
    {
        var ranking = Rank(Snapshot(products: [Boost([Sofa]), Feature([Sofa])]), Owned(Listing(Sofa, "Blue sofa")));

        var item = ranking.Items.Should().ContainSingle().Subject;

        item.Kind.Should().Be("product");
        item.Code.Should().Be("marketplace.listing.boost.7d", "the lower sort order wins among equals");
        item.SubjectType.Should().Be("MarketplaceListing");
        item.SubjectId.Should().Be(Sofa);
        item.ReasonCode.Should().Be("subject.unpromoted");
        item.ReasonKind.Should().Be(RecommendationReasonCode.OwnsUnpromotedItem);
        item.Reason.Should().Be("Nothing is promoting “Blue sofa” right now. Boost listing for 7 days runs for 7 days.");
        item.Route.Should().Be(
            $"/monetization/promote?subjectType=MarketplaceListing&subjectId={Sofa}&productCode=marketplace.listing.boost.7d");
        item.OfferId.Should().Be(MonetizationOfferKey.ForSubject("MarketplaceListing", Sofa));
    }

    [Fact]
    public void A_listing_that_is_already_promoted_is_never_suggested_another_promotion()
    {
        var snapshot = Snapshot(
            products: [Boost([Sofa]), Feature([Sofa])],
            holdings: [new MonetizationHolding("marketplace.listing.featured.7d", "MarketplaceListing", Sofa, "Scheduled", null)]);

        Rank(snapshot, Owned(Listing(Sofa, "Blue sofa"))).Items.Should().BeEmpty(
            "a scheduled placement is one the member is paying for, and \"nothing is promoting it\" would be false");
    }

    [Fact]
    public void A_product_the_rules_refuse_for_that_listing_is_not_suggested()
    {
        // Eligible for the flat only. The sofa has no eligible product, so it yields nothing.
        var ranking = Rank(Snapshot(products: [Boost([Flat])]), Owned(Listing(Sofa, "Blue sofa")));

        ranking.Items.Should().BeEmpty();
    }

    [Fact]
    public void A_product_bought_before_is_preferred_and_says_so()
    {
        var snapshot = Snapshot(
            products: [Boost([Sofa]), Feature([Sofa])],
            purchases: [new MonetizationPastPurchase("marketplace.listing.featured.7d", "FeaturedListing", "MarketplaceListing", Flat, Now.AddDays(-90))]);

        var item = Rank(snapshot, Owned(Listing(Sofa, "Blue sofa"))).Items.Should().ContainSingle().Subject;

        item.Code.Should().Be("marketplace.listing.featured.7d");
        item.ReasonCode.Should().Be("product.bought_before");
        item.Reason.Should().StartWith("You have bought Feature listing for 7 days before");
        item.Confidence.Should().BeGreaterThan(0.5m);
    }

    /// <summary>
    /// Dismissing "promote this listing" means the listing: the next product is not offered for it
    /// in the dismissed one's place.
    /// </summary>
    [Fact]
    public void A_dismissed_listing_is_not_offered_a_different_promotion_instead()
    {
        var dismissed = MonetizationOfferKey.ForSubject("MarketplaceListing", Sofa);

        var ranking = Rank(
            Snapshot(products: [Boost([Sofa]), Feature([Sofa])]),
            Owned(Listing(Sofa, "Blue sofa")),
            suppressed: [dismissed]);

        ranking.Items.Should().BeEmpty();
    }

    [Fact]
    public void Dismissing_one_listings_promotion_leaves_anothers()
    {
        var dismissed = MonetizationOfferKey.ForSubject("MarketplaceListing", Sofa);

        var ranking = Rank(
            Snapshot(products: [Boost([Sofa, Flat])]),
            Owned(Listing(Sofa, "Blue sofa"), Listing(Flat, "Desk")),
            suppressed: [dismissed]);

        ranking.Items.Should().ContainSingle().Which.SubjectId.Should().Be(Flat);
    }

    [Fact]
    public void A_member_at_their_listing_limit_is_offered_a_plan_with_more_and_told_the_numbers()
    {
        var owned = Owned(Enumerable.Range(1, 5).Select(i => Listing(Guid.NewGuid(), $"Item {i}")).ToArray());

        var ranking = Rank(Snapshot(plans: DefaultPlans()), owned);

        var plan = ranking.Items.Should().ContainSingle(item => item.Kind == "plan").Subject;

        plan.Code.Should().Be(
            "premium.monthly", "the cheapest plan whose limit exceeds the member's is the one to name");
    }

    [Fact]
    public void The_listing_limit_reason_counts_the_members_own_live_listings_against_their_plans_stated_limit()
    {
        var owned = Owned(Enumerable.Range(1, 5).Select(i => Listing(Guid.NewGuid(), $"Item {i}")).ToArray());

        var plan = Rank(Snapshot(plans: DefaultPlans()), owned).Items.Single(item => item.Kind == "plan");

        plan.ReasonCode.Should().Be("plan.near_listing_limit");
        plan.Reason.Should().Be("You have 5 live listings, and your Free plan allows 5 at once. Premium allows 25 at once.");
        plan.Route.Should().Be("/monetization/plans?highlight=premium.monthly");
        plan.Confidence.Should().Be(0.8m);
    }

    [Fact]
    public void A_member_well_under_their_limit_is_not_told_they_are_near_it()
    {
        var owned = Owned(Listing(Sofa, "Blue sofa"));

        Rank(Snapshot(plans: DefaultPlans()), owned).Items.Should().NotContain(item => item.Kind == "plan");
    }

    [Fact]
    public void The_plan_a_member_holds_is_never_recommended_nor_one_that_costs_less()
    {
        var owned = Owned(Enumerable.Range(1, 30).Select(i => Listing(Guid.NewGuid(), $"Item {i}")).ToArray());

        var ranking = Rank(Snapshot(plans: DefaultPlans(), currentPlan: "premium.monthly"), owned);

        ranking.Items.Should().NotContain(item => item.Code == "premium.monthly");
        ranking.Items.Should().NotContain(item => item.Code == "free");

        var plan = ranking.Items.Single(item => item.Kind == "plan");
        plan.Code.Should().Be("professional.monthly");
        plan.Reason.Should().Be(
            "You have 30 live listings, and your Premium plan allows 25 at once. Professional allows unlimited listings.");
        plan.Route.Should().Be("/monetization/change-plan?planCode=professional.monthly",
            "a member on a paid plan changes plan rather than choosing one");
    }

    [Fact]
    public void A_member_who_runs_a_provider_profile_is_offered_the_provider_plan()
    {
        var owned = Owned(new OwnedSubject("Provider", Clinic, "Ada's Clinic", Now.AddDays(-10)));

        var plan = Rank(Snapshot(plans: DefaultPlans()), owned).Items.Single(item => item.Kind == "plan");

        plan.Code.Should().Be("provider.monthly");
        plan.ReasonCode.Should().Be("plan.matches_provider_role");
        plan.Reason.Should().Be(
            "You run the provider profile “Ada's Clinic”, and Provider is the plan made for service providers.");
    }

    [Fact]
    public void Nobody_is_offered_a_provider_plan_without_a_provider_profile()
    {
        Rank(Snapshot(plans: DefaultPlans()), Owned()).Items.Should().BeEmpty();
    }

    [Fact]
    public void Repeated_promotion_purchases_suggest_a_plan_that_includes_boosts()
    {
        var purchases = new[]
        {
            new MonetizationPastPurchase("marketplace.listing.boost.7d", "BoostedListing", "MarketplaceListing", Sofa, Now.AddDays(-5)),
            new MonetizationPastPurchase("marketplace.listing.boost.7d", "BoostedListing", "MarketplaceListing", Flat, Now.AddDays(-20)),

            // Outside the window, and a credit bundle is not a promotion: neither counts.
            new MonetizationPastPurchase("marketplace.listing.boost.7d", "BoostedListing", "MarketplaceListing", Flat, Now.AddDays(-90)),
            new MonetizationPastPurchase("credit.bundle.25", "CreditBundle", "Member", Guid.NewGuid(), Now.AddDays(-3))
        };

        var plan = Rank(Snapshot(plans: DefaultPlans(), purchases: purchases), Owned())
            .Items.Single(item => item.Kind == "plan");

        plan.Code.Should().Be("premium.monthly");
        plan.ReasonCode.Should().Be("plan.includes_promotions_bought");
        plan.Reason.Should().Be("You bought 2 promotions in the last 60 days. Premium includes 1 boost a month.");
    }

    [Fact]
    public void Offer_keys_are_stable_and_distinguish_subjects()
    {
        MonetizationOfferKey.ForSubject("MarketplaceListing", Sofa)
            .Should().Be(MonetizationOfferKey.ForSubject("marketplacelisting", Sofa));

        MonetizationOfferKey.ForSubject("MarketplaceListing", Sofa)
            .Should().NotBe(MonetizationOfferKey.ForSubject("MarketplaceListing", Flat));

        MonetizationOfferKey.ForPlan("premium.monthly").Should().NotBe(MonetizationOfferKey.ForPlan("Professional.Monthly"));

        MonetizationOfferKey.ForPlan("premium.monthly").Should().NotBe(Guid.Empty);
    }

    // ── Builders ────────────────────────────────────────────────────────────────────────────

    private static MonetizationRanking Rank(
        MonetizationOfferSnapshot snapshot, IReadOnlyList<OwnedSubject> owned, IReadOnlyCollection<Guid>? suppressed = null) =>
        MonetizationRanker.Rank(new MonetizationRankingInput
        {
            Snapshot = snapshot,
            OwnedSubjects = owned,
            Suppressed = (suppressed ?? []).ToHashSet(),
            AsOf = Now,
            Limit = 10
        });

    private static OwnedSubject[] Owned(params OwnedSubject[] subjects) => subjects;

    private static OwnedSubject Listing(Guid id, string title) =>
        new("MarketplaceListing", id, title, Now.AddDays(-11));

    private static MonetizationOfferSnapshot Snapshot(
        IReadOnlyList<MonetizationProductOffer>? products = null,
        IReadOnlyList<MonetizationPlanOffer>? plans = null,
        IReadOnlyList<MonetizationHolding>? holdings = null,
        IReadOnlyList<MonetizationPastPurchase>? purchases = null,
        string? currentPlan = null) =>
        new()
        {
            Products = products ?? [],
            Plans = plans ?? [],
            Holdings = holdings ?? [],
            Purchases = purchases ?? [],
            CurrentPlanCode = currentPlan
        };

    private static MonetizationProductOffer Boost(IReadOnlyList<Guid> eligible) =>
        Product("marketplace.listing.boost.7d", "Boost listing for 7 days", "BoostedListing", 10, 2.99m, eligible);

    private static MonetizationProductOffer Feature(IReadOnlyList<Guid> eligible) =>
        Product("marketplace.listing.featured.7d", "Feature listing for 7 days", "FeaturedListing", 20, 4.99m, eligible);

    private static MonetizationProductOffer Product(
        string code, string name, string category, int sortOrder, decimal price, IReadOnlyList<Guid> eligible) =>
        new()
        {
            ProductId = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = name,
            Category = category,
            SubjectType = "MarketplaceListing",
            PriceMinorUnits = (long)(price * 100),
            Currency = "GBP",
            DurationDays = 7,
            SortOrder = sortOrder,
            EligibleSubjectIds = eligible
        };

    private static IReadOnlyList<MonetizationPlanOffer> DefaultPlans() =>
    [
        Plan("free", "Free", 0m, "Personal", 10, isFree: true, ("listings.concurrent", "5")),
        Plan("premium.monthly", "Premium", 7.99m, "Personal", 20, false, ("listings.concurrent", "25"), ("boosts.included.monthly", "1")),
        Plan("professional.monthly", "Professional", 19.99m, "Personal", 30, false, ("listings.concurrent", "unlimited"), ("boosts.included.monthly", "4")),
        Plan("provider.monthly", "Provider", 29.99m, "Provider", 40, false, ("services.concurrent", "unlimited")),
        Plan("organization.yearly", "Organization", 149.99m, "Organization", 50, false, ("events.concurrent", "unlimited"))
    ];

    private static MonetizationPlanOffer Plan(
        string code, string name, decimal price, string audience, int sortOrder, bool isFree,
        params (string Key, string? Value)[] entitlements) =>
        new()
        {
            PlanId = Guid.NewGuid(),
            Code = code,
            Name = name,
            Description = name,
            Tier = name,
            Interval = "Monthly",
            PriceMinorUnits = (long)(price * 100),
            Currency = "GBP",
            AudienceType = audience,
            SortOrder = sortOrder,
            IsFree = isFree,
            Entitlements = entitlements.ToDictionary(entry => entry.Key, entry => entry.Value)
        };
}
