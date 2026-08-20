using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Extensibility.Catalog;
using Diaspora.Modules.Recommendations.Infrastructure.Ranking;

namespace Diaspora.Modules.Recommendations.Catalog;

/// <summary>
/// The strategy weights the platform ships with.
/// <para>
/// <b>Every number in this file is a starting guess, and the module is built so that they can stop
/// being guesses.</b> The analytics rollup measures acceptance rate by strategy; an operator moves a
/// weight; the next week's numbers say whether it helped. That loop is the reason weights are
/// catalogue rows, and the reason these defaults are stated in one readable place rather than spread
/// across ten strategy classes as constants.
/// </para>
/// <para>
/// <b>The surfaces differ from each other in ways that are worth reading as a set.</b> Guides weight
/// recency at almost nothing, because the best answer to a visa question is often four years old.
/// Jobs weight it heavily, because a two-week-old posting is probably filled. Housing weights trust
/// above everything, because a member is about to send a deposit to a stranger. Marketplace weights
/// location hard, because nobody collects a sofa from another country. The home feed weights nothing
/// hard, because it is a mixture and a strong opinion there crowds out the variety that is the point
/// of it.
/// </para>
/// <para>
/// <b>Four strategies run on the cold-start path and six do not.</b> Popularity, recency, location
/// and trust say something about a candidate regardless of who is asking. Similarity, interest match
/// and diversity are functions of a profile and return zero for every candidate when there is none —
/// and a strategy that contributes nothing while appearing in the explanation makes the explanation
/// misleading.
/// </para>
/// </summary>
public sealed class DefaultRankingStrategyContributor : IRankingStrategyContributor
{
    public string Module => RecommendationsModuleMetadata.Name;

    public IEnumerable<RankingStrategySeed> GetStrategies()
    {
        foreach (var seed in HomeFeed()) yield return seed;
        foreach (var seed in Jobs()) yield return seed;
        foreach (var seed in Housing()) yield return seed;
        foreach (var seed in Marketplace()) yield return seed;
        foreach (var seed in Providers()) yield return seed;
        foreach (var seed in Guides()) yield return seed;
        foreach (var seed in LearningPaths()) yield return seed;
        foreach (var seed in Events()) yield return seed;
        foreach (var seed in Organizations()) yield return seed;
        foreach (var seed in Neighborhoods()) yield return seed;
        foreach (var seed in Communities()) yield return seed;
        foreach (var seed in Trending()) yield return seed;
        foreach (var seed in ContinueLearning()) yield return seed;
        foreach (var seed in Explore()) yield return seed;
        foreach (var seed in Discover()) yield return seed;
    }

    /// <summary>
    /// The mixed feed. No single strategy dominates, on purpose.
    /// <para>
    /// A home feed with a strong weight anywhere becomes a feed of one thing — and the whole value of
    /// a home screen for a diaspora member is that it shows them the guide, the job and the event in
    /// one glance.
    /// </para>
    /// </summary>
    private static IEnumerable<RankingStrategySeed> HomeFeed()
    {
        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.HomeFeed, 0.30m, "Where the member actually is.", coldStart: true, order: 10);

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.HomeFeed, 0.25m, "What the member has shown interest in.", order: 20);

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.HomeFeed, 0.15m, "What everybody nearby is engaging with.", coldStart: true, order: 30);

        yield return Seed(RecencyRankingStrategy.StrategyKey, RankingStrategyKind.Recency,
            RecommendationSurface.HomeFeed, 0.15m, "How new it is.", coldStart: true, order: 40,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RecencyRankingStrategy.HalfLifeParameter] = "21"
            });

        yield return Seed(TrustRankingStrategy.StrategyKey, RankingStrategyKind.Trust,
            RecommendationSurface.HomeFeed, 0.10m, "How much the platform can vouch for it.", coldStart: true, order: 50);

        yield return Seed(SimilarityRankingStrategy.StrategyKey, RankingStrategyKind.Similarity,
            RecommendationSurface.HomeFeed, 0.10m, "Similar to what the member has been looking at.", order: 60);

        yield return Seed(DiversityRankingStrategy.StrategyKey, RankingStrategyKind.Diversity,
            RecommendationSurface.HomeFeed, 0.10m, "Rewards a kind of thing the member has been under-served.", order: 70);

        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.HomeFeed, 0.40m, "What a new arrival needs, in order.", coldStart: true, order: 80);

        yield return Seed(RuleWeightingRankingStrategy.StrategyKey, RankingStrategyKind.RuleWeighting,
            RecommendationSurface.HomeFeed, 0.20m, "The operator's thumb on the scale.", coldStart: true, order: 90);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.HomeFeed, 0.05m, "So no page is ever empty for an arithmetic reason.", coldStart: true, order: 100);
    }

    /// <summary>
    /// Jobs. Recency matters more here than anywhere else on the platform.
    /// <para>
    /// A two-week-old posting is probably filled, and recommending it wastes a candidate's evening.
    /// The seven-day half-life is the shortest in the catalogue.
    /// </para>
    /// </summary>
    private static IEnumerable<RankingStrategySeed> Jobs()
    {
        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Jobs, 0.35m, "Skills and categories the member has engaged with.", order: 10);

        yield return Seed(RecencyRankingStrategy.StrategyKey, RankingStrategyKind.Recency,
            RecommendationSurface.Jobs, 0.30m, "A filled posting helps nobody.", coldStart: true, order: 20,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RecencyRankingStrategy.HalfLifeParameter] = "7"
            });

        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Jobs, 0.20m, "Where the member can actually work.", coldStart: true, order: 30,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // Remote work is real and a remote posting has no city. A higher floor than
                // elsewhere so remote roles are not buried under local ones.
                [LocationRankingStrategy.GlobalFloorParameter] = "0.6"
            });

        yield return Seed(TrustRankingStrategy.StrategyKey, RankingStrategyKind.Trust,
            RecommendationSurface.Jobs, 0.25m, "A verified employer is a different risk.", coldStart: true, order: 40);

        yield return Seed(SimilarityRankingStrategy.StrategyKey, RankingStrategyKind.Similarity,
            RecommendationSurface.Jobs, 0.20m, "Similar to what the member has applied for.", order: 50);

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Jobs, 0.10m, "How much attention the posting is getting.", coldStart: true, order: 60);

        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.Jobs, 0.30m, "What a new arrival needs.", coldStart: true, order: 70);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Jobs, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    /// <summary>
    /// Housing. Trust above everything, because a member is about to send a deposit to a stranger.
    /// </summary>
    private static IEnumerable<RankingStrategySeed> Housing()
    {
        yield return Seed(TrustRankingStrategy.StrategyKey, RankingStrategyKind.Trust,
            RecommendationSurface.Housing, 0.35m, "A verified landlord is a different risk.", coldStart: true, order: 10);

        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Housing, 0.30m, "Nobody rents a flat in another city by accident.", coldStart: true, order: 20,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // A listing with no location is almost certainly a data problem rather than a
                // genuinely global one. The lowest floor in the catalogue.
                [LocationRankingStrategy.GlobalFloorParameter] = "0.1"
            });

        yield return Seed(RecencyRankingStrategy.StrategyKey, RankingStrategyKind.Recency,
            RecommendationSurface.Housing, 0.25m, "A taken flat is worse than no flat.", coldStart: true, order: 30,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RecencyRankingStrategy.HalfLifeParameter] = "10"
            });

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Housing, 0.25m, "Amenities and property types the member looks at.", order: 40);

        yield return Seed(SimilarityRankingStrategy.StrategyKey, RankingStrategyKind.Similarity,
            RecommendationSurface.Housing, 0.25m, "Similar to what the member has viewed.", order: 50);

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Housing, 0.10m, "How much attention the listing is getting.", coldStart: true, order: 60);

        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.Housing, 0.25m, "What a new arrival needs.", coldStart: true, order: 70);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Housing, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    /// <summary>Marketplace. Location hardest of all — nobody collects a sofa from another country.</summary>
    private static IEnumerable<RankingStrategySeed> Marketplace()
    {
        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Marketplace, 0.35m, "Physical goods have to be collected.", coldStart: true, order: 10,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LocationRankingStrategy.GlobalFloorParameter] = "0.15"
            });

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Marketplace, 0.30m, "Categories the member browses.", order: 20);

        yield return Seed(SimilarityRankingStrategy.StrategyKey, RankingStrategyKind.Similarity,
            RecommendationSurface.Marketplace, 0.25m, "Similar to what the member has viewed.", order: 30);

        yield return Seed(TrustRankingStrategy.StrategyKey, RankingStrategyKind.Trust,
            RecommendationSurface.Marketplace, 0.25m, "A verified seller is a different risk.", coldStart: true, order: 40);

        yield return Seed(RecencyRankingStrategy.StrategyKey, RankingStrategyKind.Recency,
            RecommendationSurface.Marketplace, 0.20m, "How new the listing is.", coldStart: true, order: 50,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RecencyRankingStrategy.HalfLifeParameter] = "14"
            });

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Marketplace, 0.15m, "How much attention the listing is getting.", coldStart: true, order: 60);

        yield return Seed(DiversityRankingStrategy.StrategyKey, RankingStrategyKind.Diversity,
            RecommendationSurface.Marketplace, 0.10m, "Stops one category taking the page.", order: 70);

        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.Marketplace, 0.20m, "What a new arrival needs.", coldStart: true, order: 80);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Marketplace, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    /// <summary>Providers. Trust and proximity, because a service is a person coming to your home.</summary>
    private static IEnumerable<RankingStrategySeed> Providers()
    {
        yield return Seed(TrustRankingStrategy.StrategyKey, RankingStrategyKind.Trust,
            RecommendationSurface.Providers, 0.35m, "A verified provider is a different risk.", coldStart: true, order: 10);

        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Providers, 0.30m, "A service provider has to be reachable.", coldStart: true, order: 20);

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Providers, 0.20m, "Who the community actually uses.", coldStart: true, order: 30);

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Providers, 0.25m, "Service categories the member needs.", order: 40);

        yield return Seed(SimilarityRankingStrategy.StrategyKey, RankingStrategyKind.Similarity,
            RecommendationSurface.Providers, 0.15m, "Similar to providers the member has used.", order: 50);

        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.Providers, 0.20m, "What a new arrival needs.", coldStart: true, order: 60);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Providers, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    /// <summary>
    /// Guides. The one surface where recency is close to irrelevant.
    /// <para>
    /// A carefully maintained four-year-old explainer on residence permits is the best answer there
    /// is, and a high recency weight would bury it under a thin post from yesterday. Weighted low
    /// rather than disabled, so "recently updated" remains something the platform can honestly say.
    /// </para>
    /// </summary>
    private static IEnumerable<RankingStrategySeed> Guides()
    {
        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Guides, 0.30m, "Immigration rules are country-specific.", coldStart: true, order: 10,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                // A guide about visa applications belongs to nowhere and is useful everywhere. The
                // highest floor in the catalogue.
                [LocationRankingStrategy.GlobalFloorParameter] = "0.7"
            });

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Guides, 0.30m, "Topics the member reads.", order: 20);

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Guides, 0.25m, "What the community found useful.", coldStart: true, order: 30);

        yield return Seed(TrustRankingStrategy.StrategyKey, RankingStrategyKind.Trust,
            RecommendationSurface.Guides, 0.20m, "A verified author on an immigration question matters.", coldStart: true, order: 40);

        yield return Seed(SimilarityRankingStrategy.StrategyKey, RankingStrategyKind.Similarity,
            RecommendationSurface.Guides, 0.20m, "Similar to what the member has read.", order: 50);

        yield return Seed(RecencyRankingStrategy.StrategyKey, RankingStrategyKind.Recency,
            RecommendationSurface.Guides, 0.05m, "Barely matters — see the class summary.", coldStart: true, order: 60,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RecencyRankingStrategy.HalfLifeParameter] = "365"
            });

        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.Guides, 0.35m, "The guides a new arrival needs first.", coldStart: true, order: 70);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Guides, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    private static IEnumerable<RankingStrategySeed> LearningPaths()
    {
        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.LearningPaths, 0.40m, "Topics the member is learning about.", order: 10);

        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.LearningPaths, 0.25m, "Country-specific curricula.", coldStart: true, order: 20,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LocationRankingStrategy.GlobalFloorParameter] = "0.7"
            });

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.LearningPaths, 0.20m, "What others completed.", coldStart: true, order: 30);

        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.LearningPaths, 0.35m, "The beginner journey.", coldStart: true, order: 40);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.LearningPaths, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    /// <summary>
    /// Events. Recency means "soon" here, and it is the strongest weight on the surface.
    /// <para>
    /// An event that has already happened is not a weak recommendation, it is a wrong one — and the
    /// availability policy removes it outright. What recency does here is favour the thing happening
    /// this weekend over the thing happening in four months.
    /// </para>
    /// </summary>
    private static IEnumerable<RankingStrategySeed> Events()
    {
        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Events, 0.35m, "Nobody attends an event in another country by accident.", coldStart: true, order: 10,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LocationRankingStrategy.GlobalFloorParameter] = "0.3"
            });

        yield return Seed(RecencyRankingStrategy.StrategyKey, RankingStrategyKind.Recency,
            RecommendationSurface.Events, 0.30m, "Sooner is better. See the class summary.", coldStart: true, order: 20,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RecencyRankingStrategy.HalfLifeParameter] = "10"
            });

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Events, 0.25m, "Event categories the member attends.", order: 30);

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Events, 0.20m, "How many people are going.", coldStart: true, order: 40);

        yield return Seed(TrustRankingStrategy.StrategyKey, RankingStrategyKind.Trust,
            RecommendationSurface.Events, 0.15m, "A verified organizer.", coldStart: true, order: 50);

        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.Events, 0.20m, "What a new arrival needs.", coldStart: true, order: 60);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Events, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    private static IEnumerable<RankingStrategySeed> Organizations()
    {
        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Organizations, 0.35m, "A diaspora organization serves a place.", coldStart: true, order: 10);

        yield return Seed(TrustRankingStrategy.StrategyKey, RankingStrategyKind.Trust,
            RecommendationSurface.Organizations, 0.25m, "A verified organization.", coldStart: true, order: 20);

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Organizations, 0.25m, "How many members it has.", coldStart: true, order: 30);

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Organizations, 0.25m, "What the organization is for.", order: 40);

        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.Organizations, 0.20m, "What a new arrival needs.", coldStart: true, order: 50);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Organizations, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    private static IEnumerable<RankingStrategySeed> Neighborhoods()
    {
        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Neighborhoods, 0.50m, "A neighbourhood is a place, and nothing else about it matters as much.", coldStart: true, order: 10,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LocationRankingStrategy.GlobalFloorParameter] = "0.05"
            });

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Neighborhoods, 0.25m, "Where members are actually moving.", coldStart: true, order: 20);

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Neighborhoods, 0.20m, "What the member looks for in a place.", order: 30);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Neighborhoods, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    private static IEnumerable<RankingStrategySeed> Communities()
    {
        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Communities, 0.35m, "A diaspora community is anchored somewhere.", coldStart: true, order: 10);

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Communities, 0.30m, "How active it is.", coldStart: true, order: 20);

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Communities, 0.25m, "What the community is about.", order: 30);

        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.Communities, 0.20m, "What a new arrival needs.", coldStart: true, order: 40);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Communities, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    /// <summary>
    /// Trending. Popularity and recency, and deliberately almost nothing personal.
    /// <para>
    /// "Trending" means what is moving, not what is moving <em>for you</em>. Weighting interest match
    /// here would produce a personalised list labelled as a public one, which is a small dishonesty
    /// that undermines every other explanation the platform gives.
    /// </para>
    /// </summary>
    private static IEnumerable<RankingStrategySeed> Trending()
    {
        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Trending, 0.50m, "What is actually moving.", coldStart: true, order: 10);

        yield return Seed(RecencyRankingStrategy.StrategyKey, RankingStrategyKind.Recency,
            RecommendationSurface.Trending, 0.35m, "Trending means now.", coldStart: true, order: 20,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RecencyRankingStrategy.HalfLifeParameter] = "5"
            });

        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Trending, 0.20m, "Trending where the member is.", coldStart: true, order: 30);

        yield return Seed(DiversityRankingStrategy.StrategyKey, RankingStrategyKind.Diversity,
            RecommendationSurface.Trending, 0.10m, "Stops one thing being the whole of 'trending'.", order: 40);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Trending, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    /// <summary>
    /// Continue learning. Almost entirely interest match, because the member has already chosen.
    /// </summary>
    private static IEnumerable<RankingStrategySeed> ContinueLearning()
    {
        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.ContinueLearning, 0.60m, "What the member started.", order: 10);

        yield return Seed(SimilarityRankingStrategy.StrategyKey, RankingStrategyKind.Similarity,
            RecommendationSurface.ContinueLearning, 0.25m, "The next thing in the same vein.", order: 20);

        yield return Seed(RecencyRankingStrategy.StrategyKey, RankingStrategyKind.Recency,
            RecommendationSurface.ContinueLearning, 0.10m, "Freshness of the material.", coldStart: true, order: 30,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RecencyRankingStrategy.HalfLifeParameter] = "180"
            });

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.ContinueLearning, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    /// <summary>Explore Diaspora — the product-education surface.</summary>
    private static IEnumerable<RankingStrategySeed> Explore()
    {
        yield return Seed(ColdStartRankingStrategy.StrategyKey, RankingStrategyKind.ColdStart,
            RecommendationSurface.Explore, 0.45m, "Explore exists for people who have not explored.", coldStart: true, order: 10);

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Explore, 0.25m, "What the member has not learned yet.", order: 20);

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Explore, 0.20m, "What others found worth reading.", coldStart: true, order: 30);

        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Explore, 0.15m, "Country-specific product guidance.", coldStart: true, order: 40,
            parameters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [LocationRankingStrategy.GlobalFloorParameter] = "0.8"
            });

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Explore, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    /// <summary>
    /// Discover. Configured but rarely generated — this module contributes boost signals to search
    /// rather than a page, and these weights exist so that the "recommended" sort option on the
    /// discover screen agrees with the rest of the platform.
    /// </summary>
    private static IEnumerable<RankingStrategySeed> Discover()
    {
        yield return Seed(LocationRankingStrategy.StrategyKey, RankingStrategyKind.Location,
            RecommendationSurface.Discover, 0.30m, "Where the member is.", coldStart: true, order: 10);

        yield return Seed(InterestMatchRankingStrategy.StrategyKey, RankingStrategyKind.InterestMatch,
            RecommendationSurface.Discover, 0.30m, "What the member is interested in.", order: 20);

        yield return Seed(TrustRankingStrategy.StrategyKey, RankingStrategyKind.Trust,
            RecommendationSurface.Discover, 0.20m, "What the platform can vouch for.", coldStart: true, order: 30);

        yield return Seed(PopularityRankingStrategy.StrategyKey, RankingStrategyKind.Popularity,
            RecommendationSurface.Discover, 0.20m, "What everybody is engaging with.", coldStart: true, order: 40);

        yield return Seed(FallbackRankingStrategy.StrategyKey, RankingStrategyKind.Fallback,
            RecommendationSurface.Discover, 0.05m, "So the page is never empty.", coldStart: true, order: 100);
    }

    private static RankingStrategySeed Seed(
        string key,
        RankingStrategyKind kind,
        RecommendationSurface surface,
        decimal weight,
        string description,
        bool coldStart = false,
        int order = 100,
        IReadOnlyDictionary<string, string>? parameters = null) =>
        new()
        {
            Key = key,
            Kind = kind,
            Surface = surface,
            Weight = weight,
            Description = description,
            RunsOnColdStart = coldStart,
            Order = order,
            Parameters = parameters ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };
}
