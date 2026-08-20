using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Community;

/// <summary>
/// Community's content-type vocabulary, translated into this module's target types.
/// <para>
/// <b>Community's contracts carry a <c>ContentType</c> string that spans every kind of thing in the
/// platform</b> — a bookmark can be on a guide, a listing, a flat or a post — so a single consumer
/// has to translate one string into a target type. Declared once rather than inline in three
/// consumers, because a mapping written three times is a mapping that will differ in one of them and
/// the symptom would be bookmarks on one content type silently landing in the wrong affinity.
/// </para>
/// <para>
/// <b>Unrecognised types return null and the consumer does nothing.</b> Community knows about
/// content types this module has no target type for — a post, a comment — and inventing one would
/// put rows in the candidate table for things no surface can render.
/// </para>
/// </summary>
internal static class ContentTypeMap
{
    public static RecommendationTargetType? Translate(string? contentType) =>
        contentType?.Trim().ToLowerInvariant() switch
        {
            "guide" => RecommendationTargetType.Guide,
            "job" or "jobposting" => RecommendationTargetType.Job,
            "listing" or "marketplacelisting" => RecommendationTargetType.MarketplaceListing,
            "housing" or "housinglisting" or "property" => RecommendationTargetType.HousingListing,
            "provider" => RecommendationTargetType.Provider,
            "event" or "communityevent" => RecommendationTargetType.Event,
            "organization" or "organisation" => RecommendationTargetType.Organization,
            "community" => RecommendationTargetType.Community,
            "neighborhood" or "neighbourhood" or "area" => RecommendationTargetType.Neighborhood,
            "learningpath" => RecommendationTargetType.LearningPath,

            // A post, a comment, anything else. See the class summary.
            _ => null
        };
}
