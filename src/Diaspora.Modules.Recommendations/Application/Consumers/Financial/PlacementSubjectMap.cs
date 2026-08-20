using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Financial;

/// <summary>
/// The Financial Platform's subject vocabulary, translated into this module's target types.
/// <para>
/// <b>B15 models one placement product across ten subject types rather than a product per module</b>,
/// which is the same decision the client made when it drew "Boost" as one action across eight content
/// types. So one consumer translates one string, exactly as Community's does.
/// </para>
/// <para>
/// <b>Two of B15's subjects are deliberately unmapped and the omissions are the interesting part.</b>
/// <c>Member</c> is what B15 uses for a purchase that is about the buyer rather than about a thing —
/// a subscription — and a subscription is not a recommendable object. <c>Update</c> is a community
/// post, which this module has no target type for and inventing one would put rows in the candidate
/// pool that no surface can render. Both return null and the consumer does nothing, which is the
/// same contract <c>ContentTypeMap</c> keeps.
/// </para>
/// </summary>
internal static class PlacementSubjectMap
{
    public static RecommendationTargetType? Translate(string? subjectType) =>
        subjectType?.Trim().ToLowerInvariant() switch
        {
            "marketplacelisting" => RecommendationTargetType.MarketplaceListing,
            "housinglisting" => RecommendationTargetType.HousingListing,
            "job" => RecommendationTargetType.Job,
            "provider" => RecommendationTargetType.Provider,
            "organization" => RecommendationTargetType.Organization,
            "event" => RecommendationTargetType.Event,
            "guide" => RecommendationTargetType.Guide,
            "community" => RecommendationTargetType.Community,

            // Member (a subscription) and Update (a post). See the class summary.
            _ => null
        };
}
