using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Feed;

namespace Diaspora.Modules.Recommendations.Catalog;

/// <summary>
/// The home feed the platform ships with.
/// <para>
/// <b>The order and the section keys are taken from the mobile client's existing home screen, not
/// invented here.</b> The app already renders "Trending Guides" as a horizontal carousel above
/// "Community Updates" as a vertical list; a backend that returned its own ordering with its own
/// names would force a mapping layer on the client and the two would drift the first time either
/// changed. The keys are what the client switches its layout on.
/// </para>
/// <para>
/// <b>Guides lead, and that is the same product judgement the cold-start ladder makes.</b> Somebody
/// who has just moved country needs to understand the paperwork before they need a sofa. It is a
/// judgement rather than a measurement, and it is one <c>Order</c> value away from being a different
/// one.
/// </para>
/// <para>
/// <b>Continue Learning is the only section that does not show on cold start.</b> A member with no
/// history has nothing to continue, and a section titled "Continue where you left off" containing
/// three guides they have never opened is worse than an absent row.
/// </para>
/// </summary>
public sealed class DefaultFeedSectionContributor : IFeedSectionContributor
{
    public string Module => RecommendationsModuleMetadata.Name;

    public IEnumerable<FeedSectionDefinition> GetSections()
    {
        yield return new FeedSectionDefinition
        {
            Key = "for-you",
            Title = "For you",
            TitleResourceKey = "recommendations.feed.for_you",
            Surface = RecommendationSurface.HomeFeed,
            Order = 10,
            Limit = 8,

            // Mixed types, and personal by definition. Omitted entirely for a member who opted out —
            // a "For you" row that is not personalised is a heading that is not true.
            RequiresPersonalization = true,
            MinimumItems = 3,
            SeeAllRoute = "/discover"
        };

        yield return new FeedSectionDefinition
        {
            Key = "trending-guides",
            Title = "Trending guides",
            TitleResourceKey = "recommendations.feed.trending_guides",
            TargetType = RecommendationTargetType.Guide,
            Surface = RecommendationSurface.Guides,
            Order = 20,
            Limit = 6,
            MinimumItems = 2,
            SeeAllRoute = "/knowledge/guides"
        };

        yield return new FeedSectionDefinition
        {
            Key = "continue-learning",
            Title = "Continue learning",
            TitleResourceKey = "recommendations.feed.continue_learning",
            TargetType = RecommendationTargetType.LearningPath,
            Surface = RecommendationSurface.ContinueLearning,
            Order = 30,
            Limit = 4,
            RequiresPersonalization = true,

            // Nothing to continue. See the class summary.
            ShowOnColdStart = false,
            MinimumItems = 1,
            SeeAllRoute = "/knowledge/learning-paths"
        };

        yield return new FeedSectionDefinition
        {
            Key = "housing-near-you",
            Title = "Housing near you",
            TitleResourceKey = "recommendations.feed.housing_near_you",
            TargetType = RecommendationTargetType.HousingListing,
            Surface = RecommendationSurface.Housing,
            Order = 40,
            Limit = 6,
            MinimumItems = 2,
            SeeAllRoute = "/housing"
        };

        yield return new FeedSectionDefinition
        {
            Key = "jobs-for-you",
            Title = "Jobs for you",
            TitleResourceKey = "recommendations.feed.jobs_for_you",
            TargetType = RecommendationTargetType.Job,
            Surface = RecommendationSurface.Jobs,
            Order = 50,
            Limit = 6,
            MinimumItems = 2,
            SeeAllRoute = "/job"
        };

        yield return new FeedSectionDefinition
        {
            Key = "events-near-you",
            Title = "Happening near you",
            TitleResourceKey = "recommendations.feed.events_near_you",
            TargetType = RecommendationTargetType.Event,
            Surface = RecommendationSurface.Events,
            Order = 60,
            Limit = 6,
            MinimumItems = 1,
            SeeAllRoute = "/community-life"
        };

        yield return new FeedSectionDefinition
        {
            Key = "trusted-providers",
            Title = "Trusted providers",
            TitleResourceKey = "recommendations.feed.trusted_providers",
            TargetType = RecommendationTargetType.Provider,
            Surface = RecommendationSurface.Providers,
            Order = 70,
            Limit = 6,
            MinimumItems = 2,
            SeeAllRoute = "/provider"
        };

        yield return new FeedSectionDefinition
        {
            Key = "marketplace-near-you",
            Title = "In the marketplace",
            TitleResourceKey = "recommendations.feed.marketplace_near_you",
            TargetType = RecommendationTargetType.MarketplaceListing,
            Surface = RecommendationSurface.Marketplace,
            Order = 80,
            Limit = 6,
            MinimumItems = 2,
            SeeAllRoute = "/marketplace"
        };

        yield return new FeedSectionDefinition
        {
            Key = "organizations",
            Title = "Communities to join",
            TitleResourceKey = "recommendations.feed.organizations",
            TargetType = RecommendationTargetType.Organization,
            Surface = RecommendationSurface.Organizations,
            Order = 90,
            Limit = 4,
            MinimumItems = 1,
            SeeAllRoute = "/community-life/org"
        };
    }
}
