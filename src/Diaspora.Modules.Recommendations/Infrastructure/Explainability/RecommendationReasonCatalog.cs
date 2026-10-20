using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Infrastructure.Explainability;

/// <summary>
/// The translation key and English fallback for every reason code.
/// <para>
/// <b>One table, and it is the only place in the backend that contains a sentence a member will
/// read.</b> Keeping it in one file means the whole set of things the platform will ever say about
/// why it recommended something can be reviewed in one sitting — which matters, because these
/// sentences are the entire trust surface of the module and it is very easy to write one that
/// oversells.
/// </para>
/// <para>
/// <b>The English is a fallback, not the product.</b> The client renders from the resource key; this
/// is what a client shows when it meets a code its translation bundle predates. Every string here is
/// deliberately plain — no exclamation, no "just for you" — because a fallback that sounds marketed
/// is worse than one that sounds mechanical.
/// </para>
/// <para>
/// <b>The <c>{0}</c> placeholder is filled with the reason detail</b> — a city, a category, a skill.
/// A code whose sentence has no placeholder ignores the detail rather than appending it, which is
/// why the two forms are stored together rather than composed.
/// </para>
/// </summary>
public static class RecommendationReasonCatalog
{
    private const string Prefix = "recommendations.reason.";

    private static readonly Dictionary<RecommendationReasonCode, (string Key, string Text)> Entries = new()
    {
        [RecommendationReasonCode.Unspecified] = ("unspecified", "Suggested for you."),

        [RecommendationReasonCode.LivesInCountry] = ("lives_in_country", "Because you live in {0}."),
        [RecommendationReasonCode.LivesInCity] = ("lives_in_city", "Because you live in {0}."),
        [RecommendationReasonCode.LivesInNeighborhood] = ("lives_in_neighborhood", "In your neighbourhood, {0}."),
        [RecommendationReasonCode.NearYou] = ("near_you", "Near you."),
        [RecommendationReasonCode.MovingToCountry] = ("moving_to_country", "Because you're interested in {0}."),
        [RecommendationReasonCode.PresentInCountry] = ("present_in_country", "Active in {0}."),

        [RecommendationReasonCode.SpeaksLanguage] = ("speaks_language", "In a language you read."),
        [RecommendationReasonCode.MatchesInterests] = ("matches_interests", "Matches your interests."),
        [RecommendationReasonCode.MatchesCategory] = ("matches_category", "Because you're interested in {0}."),
        [RecommendationReasonCode.MatchesSkills] = ("matches_skills", "Matches your {0} experience."),
        [RecommendationReasonCode.NewToPlatform] = ("new_to_platform", "A good place to start."),

        [RecommendationReasonCode.RecentlyViewed] = ("recently_viewed", "You looked at this recently."),
        [RecommendationReasonCode.ViewedSimilar] = ("viewed_similar", "Similar to what you've been viewing."),
        [RecommendationReasonCode.SavedSimilar] = ("saved_similar", "Similar to something you saved."),
        [RecommendationReasonCode.AppliedToSimilar] = ("applied_to_similar", "Similar to what you've applied for."),
        [RecommendationReasonCode.SearchedFor] = ("searched_for", "Because you searched for {0}."),
        [RecommendationReasonCode.JoinedCommunity] = ("joined_community", "Because you joined {0}."),
        [RecommendationReasonCode.MemberOfOrganization] = ("member_of_organization", "From {0}, which you belong to."),
        [RecommendationReasonCode.AttendedEvent] = ("attended_event", "Because you attended {0}."),
        [RecommendationReasonCode.FollowedAuthor] = ("followed_author", "From someone you follow."),
        [RecommendationReasonCode.CompletedGuides] = ("completed_guides", "Because you finished {0}."),
        [RecommendationReasonCode.ContinueWhereYouLeftOff] = ("continue_learning", "Continue where you left off."),
        [RecommendationReasonCode.TransactedInCategory] = ("transacted_in_category", "Because of your activity in {0}."),

        [RecommendationReasonCode.PopularInNeighborhood] = ("popular_in_neighborhood", "Popular in {0}."),
        [RecommendationReasonCode.PopularInCity] = ("popular_in_city", "Popular in {0}."),
        [RecommendationReasonCode.PopularInCountry] = ("popular_in_country", "Popular in {0}."),
        [RecommendationReasonCode.TrendingAmongVerified] = ("trending_among_verified", "Trending among verified members."),
        [RecommendationReasonCode.TrendingNow] = ("trending_now", "Trending now."),

        [RecommendationReasonCode.VerifiedProvider] = ("verified_provider", "Verified by Diaspora."),
        [RecommendationReasonCode.HighlyRated] = ("highly_rated", "Highly rated by members."),
        [RecommendationReasonCode.RecentlyPublished] = ("recently_published", "Recently published."),
        [RecommendationReasonCode.RecentlyUpdated] = ("recently_updated", "Recently updated."),

        [RecommendationReasonCode.PopularWithNewArrivals] = ("popular_with_new_arrivals", "Popular with people who've just arrived."),
        [RecommendationReasonCode.StartHere] = ("start_here", "A good place to start."),

        // The monetization surface writes its own sentence for each item — it names the listing and
        // the numbers — and these are the generic fallbacks a client without that text would show.
        [RecommendationReasonCode.OwnsUnpromotedItem] = ("owns_unpromoted_item", "Nothing is promoting {0} right now."),
        [RecommendationReasonCode.BoughtBefore] = ("bought_before", "You have bought this before."),
        [RecommendationReasonCode.NearPlanLimit] = ("near_plan_limit", "You are close to your plan's listing limit."),
        [RecommendationReasonCode.BuysPromotionsOften] = ("buys_promotions_often", "This plan includes promotions you have been buying."),
        [RecommendationReasonCode.MatchesRole] = ("matches_role", "Made for {0}.")
    };

    /// <summary>The translation key for a code. Always resolvable.</summary>
    public static string ResourceKeyFor(RecommendationReasonCode code) =>
        Prefix + (Entries.TryGetValue(code, out var entry)
            ? entry.Key
            : Entries[RecommendationReasonCode.Unspecified].Key);

    /// <summary>
    /// The English rendering, with the detail substituted.
    /// <para>
    /// A missing detail on a sentence that expects one falls back to the generic phrasing rather
    /// than rendering an empty placeholder. "Because you live in ." is the kind of thing that
    /// escapes to production because nobody tests the null case of a fallback string.
    /// </para>
    /// </summary>
    public static string TextFor(RecommendationReasonCode code, string? detail)
    {
        var template = Entries.TryGetValue(code, out var entry)
            ? entry.Text
            : Entries[RecommendationReasonCode.Unspecified].Text;

        if (!template.Contains("{0}", StringComparison.Ordinal))
        {
            return template;
        }

        return string.IsNullOrWhiteSpace(detail)
            ? Entries[RecommendationReasonCode.MatchesInterests].Text
            : string.Format(System.Globalization.CultureInfo.InvariantCulture, template, detail.Trim());
    }

    /// <summary>Every code the catalogue can render. What the reasons endpoint returns.</summary>
    public static IReadOnlyCollection<RecommendationReasonCode> KnownCodes => Entries.Keys;

    /// <summary>
    /// The raw English template, placeholder and all.
    /// <para>
    /// Unsubstituted on purpose: a client building a translation table needs to know which reasons
    /// take a detail, and a rendered example would not tell it.
    /// </para>
    /// </summary>
    public static string TemplateFor(RecommendationReasonCode code) =>
        Entries.TryGetValue(code, out var entry)
            ? entry.Text
            : Entries[RecommendationReasonCode.Unspecified].Text;
}
