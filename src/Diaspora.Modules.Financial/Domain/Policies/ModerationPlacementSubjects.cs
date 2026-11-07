using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Financial.Domain.Enums;

namespace Diaspora.Modules.Financial.Domain.Policies;

/// <summary>
/// What a placement calls the content a moderation case is about (7 Nov 2026).
/// <para>
/// A case names its content by <see cref="ModerationContentTypes"/>; a placement names its subject by
/// <see cref="MonetizationSubjectType"/>. Written out rather than parsed by name, so a content type
/// added on either side is a decision here instead of a silently unmatched pause. Content nothing
/// sells a placement over (fundraisers, files, posts and comments) maps to nothing.
/// </para>
/// </summary>
public static class ModerationPlacementSubjects
{
    public static MonetizationSubjectType? Map(string? contentType) => contentType switch
    {
        ModerationContentTypes.Job => MonetizationSubjectType.Job,
        ModerationContentTypes.MarketplaceListing => MonetizationSubjectType.MarketplaceListing,
        ModerationContentTypes.HousingListing => MonetizationSubjectType.HousingListing,
        ModerationContentTypes.Provider => MonetizationSubjectType.Provider,
        ModerationContentTypes.Event => MonetizationSubjectType.Event,
        ModerationContentTypes.Organization => MonetizationSubjectType.Organization,
        ModerationContentTypes.Guide => MonetizationSubjectType.Guide,

        // A fundraising campaign is not MonetizationSubjectType.Campaign, which is an advertising
        // campaign; no product promotes a fundraiser, a file, a post or a comment.
        _ => null
    };
}
