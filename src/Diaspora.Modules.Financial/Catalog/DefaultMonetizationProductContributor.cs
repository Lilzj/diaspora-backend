using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Extensibility.Catalog;

namespace Diaspora.Modules.Financial.Catalog;

/// <summary>
/// The products the platform ships, one per monetizable surface the brief names.
/// <para>
/// <b>Every "premium feature" on this platform is a row in this list, not a code path.</b> Premium
/// and featured listings, boosted jobs and marketplace listings, featured providers and
/// organizations, featured events, housing highlights, creator promotions, credit bundles — each is
/// a catalogue entry with a price, a duration, a placement kind and a priority weight. Nothing in
/// this module switches on a product code, which is what makes "no hardcoded premium features" a
/// structural fact rather than a claim.
/// </para>
/// <para>
/// <b>Prices are in GBP and are starting values.</b> A deployment reprices through the catalogue or
/// moves a product onto a pricing policy for country overrides; neither is a release. The seeder
/// creates and never overwrites, so a repricing survives a restart.
/// </para>
/// <para>
/// <b>Priority weights are deliberately modest.</b> A boost that multiplied a candidate's score by
/// ten would make paid placement the only thing anybody ever saw, which is a product decision the
/// Recommendation Platform should be able to make rather than one this catalogue imposes — B14
/// decides what to do with the weight, and this module never learns what it did.
/// </para>
/// </summary>
public sealed class DefaultMonetizationProductContributor : IMonetizationProductContributor
{
    private const string Gbp = "GBP";

    public string Module => FinancialModuleMetadata.Name;

    public IEnumerable<MonetizationProductSeed> GetProducts()
    {
        // ── Advertising campaigns — B16 ──────────────────────────────────────────────────────
        //
        // Fixed tiers rather than a budget the advertiser types, and the reason is the payment
        // path: a purchase is priced from the catalogue or from a pricing policy over a catalogue
        // amount, and neither takes a figure from the request. Charging an arbitrary budget would
        // mean changing the shape every product on this platform is bought through, on the one
        // path where a mistake takes somebody's money.
        //
        // A tier *is* the budget. The campaign's budget must equal what its product charges, which
        // is checked at submission — one number, not two that can disagree.
        //
        // The subject is the campaign itself, which is what lets a captured purchase find its way
        // back to it. Placement kind and priority weight are deliberately absent — a campaign books
        // its placements through its own lifecycle, not through the purchase.

        yield return new MonetizationProductSeed
        {
            Code = "advertising.campaign.starter.30d",
            Name = "Starter campaign",
            Description = "A 30-day advertising campaign with a £25 budget.",
            Category = MonetizationProductCategory.Campaign,
            SubjectType = MonetizationSubjectType.Campaign,
            PriceAmount = 25.00m,
            Currency = Gbp,
            DurationDays = 30,
            SortOrder = 500,
            IsSystem = true
        };

        yield return new MonetizationProductSeed
        {
            Code = "advertising.campaign.standard.30d",
            Name = "Standard campaign",
            Description = "A 30-day advertising campaign with a £50 budget.",
            Category = MonetizationProductCategory.Campaign,
            SubjectType = MonetizationSubjectType.Campaign,
            PriceAmount = 50.00m,
            Currency = Gbp,
            DurationDays = 30,
            SortOrder = 510,
            IsSystem = true
        };

        yield return new MonetizationProductSeed
        {
            Code = "advertising.campaign.growth.30d",
            Name = "Growth campaign",
            Description = "A 30-day advertising campaign with a £100 budget.",
            Category = MonetizationProductCategory.Campaign,
            SubjectType = MonetizationSubjectType.Campaign,
            PriceAmount = 100.00m,
            Currency = Gbp,
            DurationDays = 30,
            SortOrder = 520,
            IsSystem = true
        };

        yield return new MonetizationProductSeed
        {
            Code = "advertising.campaign.scale.30d",
            Name = "Scale campaign",
            Description = "A 30-day advertising campaign with a £250 budget.",
            Category = MonetizationProductCategory.Campaign,
            SubjectType = MonetizationSubjectType.Campaign,
            PriceAmount = 250.00m,
            Currency = Gbp,
            DurationDays = 30,
            SortOrder = 530,
            IsSystem = true
        };

        // ── Marketplace ──────────────────────────────────────────────────────────────────────

        yield return new MonetizationProductSeed
        {
            Code = "marketplace.listing.boost.7d",
            Name = "Boost listing for 7 days",
            Description = "Increase search priority and local reach for a week.",
            Category = MonetizationProductCategory.BoostedListing,
            SubjectType = MonetizationSubjectType.MarketplaceListing,
            PriceAmount = 4.99m,
            Currency = Gbp,
            DurationDays = 7,
            PlacementKind = PlacementKind.Boost,
            PriorityWeight = 1.5m,
            SortOrder = 10,
            IsSystem = true
        };

        yield return new MonetizationProductSeed
        {
            Code = "marketplace.listing.featured.7d",
            Name = "Feature listing for 7 days",
            Description = "Appear in the featured rail for a week.",
            Category = MonetizationProductCategory.FeaturedListing,
            SubjectType = MonetizationSubjectType.MarketplaceListing,
            PriceAmount = 9.99m,
            Currency = Gbp,
            DurationDays = 7,
            PlacementKind = PlacementKind.Featured,
            PriorityWeight = 2m,
            SortOrder = 20,
            IsSystem = true
        };

        yield return new MonetizationProductSeed
        {
            Code = "marketplace.listing.premium.30d",
            Name = "Premium listing for 30 days",
            Description = "Featured placement, raised priority and the premium badge for a month.",
            Category = MonetizationProductCategory.PremiumListing,
            SubjectType = MonetizationSubjectType.MarketplaceListing,
            PriceAmount = 24.99m,
            Currency = Gbp,
            DurationDays = 30,
            PlacementKind = PlacementKind.Premium,
            PriorityWeight = 2.5m,
            SortOrder = 30,
            IsSystem = true
        };

        // ── Jobs ─────────────────────────────────────────────────────────────────────────────

        yield return new MonetizationProductSeed
        {
            Code = "jobs.posting.boost.14d",
            Name = "Boost job posting for 14 days",
            Description = "Raise a posting's priority in search and recommendations for a fortnight.",
            Category = MonetizationProductCategory.BoostedJob,
            SubjectType = MonetizationSubjectType.Job,
            PriceAmount = 19.99m,
            Currency = Gbp,
            DurationDays = 14,
            PlacementKind = PlacementKind.Boost,
            PriorityWeight = 1.8m,
            SortOrder = 40,
            IsSystem = true
        };

        yield return new MonetizationProductSeed
        {
            Code = "jobs.posting.featured.30d",
            Name = "Feature job posting for 30 days",
            Description = "Appear in the featured jobs rail for a month.",
            Category = MonetizationProductCategory.FeaturedListing,
            SubjectType = MonetizationSubjectType.Job,
            PriceAmount = 49.99m,
            Currency = Gbp,
            DurationDays = 30,
            PlacementKind = PlacementKind.Featured,
            PriorityWeight = 2m,
            SortOrder = 50,
            IsSystem = true
        };

        // ── Housing ──────────────────────────────────────────────────────────────────────────

        yield return new MonetizationProductSeed
        {
            Code = "housing.listing.highlight.14d",
            Name = "Highlight property for 14 days",
            Description = "A highlighted card and raised priority in the neighbourhood for a fortnight.",
            Category = MonetizationProductCategory.HousingHighlight,
            SubjectType = MonetizationSubjectType.HousingListing,
            PriceAmount = 14.99m,
            Currency = Gbp,
            DurationDays = 14,
            PlacementKind = PlacementKind.Spotlight,
            PriorityWeight = 1.6m,
            SortOrder = 60,
            IsSystem = true
        };

        yield return new MonetizationProductSeed
        {
            Code = "housing.listing.premium.30d",
            Name = "Premium property listing for 30 days",
            Description = "Featured placement and raised priority for a month.",
            Category = MonetizationProductCategory.PremiumListing,
            SubjectType = MonetizationSubjectType.HousingListing,
            PriceAmount = 39.99m,
            Currency = Gbp,
            DurationDays = 30,
            PlacementKind = PlacementKind.Premium,
            PriorityWeight = 2.2m,
            SortOrder = 70,
            IsSystem = true
        };

        // ── Providers ────────────────────────────────────────────────────────────────────────

        yield return new MonetizationProductSeed
        {
            Code = "providers.profile.featured.30d",
            Name = "Feature provider for 30 days",
            Description = "Appear in the featured providers rail in your country for a month.",
            Category = MonetizationProductCategory.FeaturedProvider,
            SubjectType = MonetizationSubjectType.Provider,
            PriceAmount = 29.99m,
            Currency = Gbp,
            DurationDays = 30,
            PlacementKind = PlacementKind.Featured,
            PriorityWeight = 2m,
            SortOrder = 80,
            IsSystem = true,

            // A featured provider is a recommendation the platform is making with its own voice.
            // Making it available only to a verified provider is the one eligibility rule in this
            // catalogue that is about trust rather than about money.
            Eligibility = new Dictionary<EligibilityRuleKind, string>
            {
                [EligibilityRuleKind.VerifiedOnly] = "true"
            }
        };

        yield return new MonetizationProductSeed
        {
            Code = "providers.profile.premium.90d",
            Name = "Premium provider for 90 days",
            Description = "Featured placement, raised ranking and the premium badge for a quarter.",
            Category = MonetizationProductCategory.PremiumProvider,
            SubjectType = MonetizationSubjectType.Provider,
            PriceAmount = 79.99m,
            Currency = Gbp,
            DurationDays = 90,
            PlacementKind = PlacementKind.Premium,
            PriorityWeight = 2.5m,
            SortOrder = 90,
            IsSystem = true,
            Eligibility = new Dictionary<EligibilityRuleKind, string>
            {
                [EligibilityRuleKind.VerifiedOnly] = "true"
            }
        };

        // ── Organizations and events ─────────────────────────────────────────────────────────

        yield return new MonetizationProductSeed
        {
            Code = "organizations.profile.featured.30d",
            Name = "Feature organization for 30 days",
            Description = "Appear in the featured organizations directory for a month.",
            Category = MonetizationProductCategory.FeaturedOrganization,
            SubjectType = MonetizationSubjectType.Organization,
            PriceAmount = 19.99m,
            Currency = Gbp,
            DurationDays = 30,
            PlacementKind = PlacementKind.Featured,
            PriorityWeight = 1.8m,
            SortOrder = 100,
            IsSystem = true
        };

        yield return new MonetizationProductSeed
        {
            Code = "events.listing.featured.14d",
            Name = "Feature event for 14 days",
            Description = "Appear in the featured events rail until the event runs.",
            Category = MonetizationProductCategory.FeaturedEvent,
            SubjectType = MonetizationSubjectType.Event,
            PriceAmount = 9.99m,
            Currency = Gbp,
            DurationDays = 14,
            PlacementKind = PlacementKind.Featured,
            PriorityWeight = 1.8m,
            SortOrder = 110,
            IsSystem = true
        };

        // ── Creator ──────────────────────────────────────────────────────────────────────────

        yield return new MonetizationProductSeed
        {
            Code = "guides.promotion.30d",
            Name = "Promote guide for 30 days",
            Description = "Raise a guide's priority in knowledge recommendations for a month.",
            Category = MonetizationProductCategory.CreatorPromotion,
            SubjectType = MonetizationSubjectType.Guide,
            PriceAmount = 14.99m,
            Currency = Gbp,
            DurationDays = 30,
            PlacementKind = PlacementKind.Boost,
            PriorityWeight = 1.5m,
            SortOrder = 120,
            IsSystem = true
        };

        // ── Credit ───────────────────────────────────────────────────────────────────────────

        yield return new MonetizationProductSeed
        {
            Code = "credit.bundle.25",
            Name = "£25 credit bundle",
            Description = "Twenty-five pounds of platform credit, spendable on anything here.",
            Category = MonetizationProductCategory.CreditBundle,
            SubjectType = MonetizationSubjectType.Member,
            PriceAmount = 25.00m,
            Currency = Gbp,
            IsRenewable = true,
            SortOrder = 200,
            IsSystem = true
        };

        yield return new MonetizationProductSeed
        {
            Code = "credit.bundle.100",
            Name = "£100 credit bundle",
            Description = "One hundred pounds of platform credit, spendable on anything here.",
            Category = MonetizationProductCategory.CreditBundle,
            SubjectType = MonetizationSubjectType.Member,
            PriceAmount = 100.00m,
            Currency = Gbp,
            IsRenewable = true,
            SortOrder = 210,
            IsSystem = true
        };
    }
}
