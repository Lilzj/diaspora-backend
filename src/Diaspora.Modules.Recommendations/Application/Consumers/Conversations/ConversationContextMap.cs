using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Application.Consumers.Conversations;

/// <summary>
/// The conversation catalogue's context vocabulary, translated into this module's target types.
/// <para>
/// The same shape as <c>PlacementSubjectMap</c> and <c>ContentTypeMap</c>: one string in, an
/// optional target type out, and a null that means the consumer does nothing. The keys are the
/// twelve the client already knows, and they are reference data rather than an enum — a vertical
/// that contributes a thirteenth needs no change here, it simply produces no signal until somebody
/// decides what kind of thing it is.
/// </para>
/// <para>
/// <b>Four of the twelve are deliberately unmapped, and the omissions carry the argument.</b>
/// <c>PrivateConversation</c> is two members about nothing in particular — there is no entity and
/// this module has no member target type. <c>BusinessAccount</c> is a member talking to a business,
/// which is not the Provider directory. <c>SupportTicket</c>, <c>TrustAppeal</c> and
/// <c>AiAssistant</c> are system-managed: opening one is not an expression of interest in anything,
/// and treating a member's appeal against a moderation decision as an affinity signal would be
/// grotesque as well as wrong.
/// </para>
/// </summary>
internal static class ConversationContextMap
{
    public static RecommendationTargetType? Translate(string? contextType) =>
        contextType?.Trim().ToLowerInvariant() switch
        {
            "marketplacelisting" => RecommendationTargetType.MarketplaceListing,
            "housinglisting" => RecommendationTargetType.HousingListing,
            "jobopportunity" => RecommendationTargetType.Job,
            "providerinquiry" => RecommendationTargetType.Provider,
            "communityevent" => RecommendationTargetType.Event,
            "organization" => RecommendationTargetType.Organization,
            "communityguide" => RecommendationTargetType.Guide,

            // PrivateConversation, BusinessAccount, SupportTicket, TrustAppeal, AiAssistant.
            // See the class summary.
            _ => null
        };
}
