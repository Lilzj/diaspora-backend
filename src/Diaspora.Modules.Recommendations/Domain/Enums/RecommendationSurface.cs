namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// Where a recommendation is going to be shown.
/// <para>
/// <b>A surface is not a target type, and conflating the two is the mistake this enum exists to
/// prevent.</b> The home feed shows guides, jobs, events and housing together; the jobs surface
/// shows only jobs. Strategy weights, policies and diversity rules are all configured per surface,
/// because "recency matters more than popularity" is true on the home feed and false on the guides
/// surface where a five-year-old visa explainer is the best answer there is.
/// </para>
/// <para>
/// The names match the client's screens, because a weight nobody can locate on a screen is a weight
/// nobody will ever tune correctly.
/// </para>
/// </summary>
public enum RecommendationSurface
{
    /// <summary>The client's first screen. Mixed target types, section-shaped.</summary>
    HomeFeed = 0,

    /// <summary>Explore Diaspora — the product-education surface.</summary>
    Explore = 1,

    /// <summary>The search and browse surface. This module contributes boost signals only.</summary>
    Discover = 2,

    Jobs = 3,
    Marketplace = 4,
    Housing = 5,
    Providers = 6,
    Guides = 7,
    LearningPaths = 8,
    Events = 9,
    Organizations = 10,
    Neighborhoods = 11,
    Communities = 12,

    /// <summary>What is moving right now, across target types.</summary>
    Trending = 13,

    /// <summary>Where the member left off. Learning only, and ordered by their progress rather than by score.</summary>
    ContinueLearning = 14
}
