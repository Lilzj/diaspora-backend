using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Community.Domain.Enums;

namespace Diaspora.Modules.Community.Domain.Policies;

/// <summary>
/// A Community report, in the platform's moderation vocabulary (7 Nov 2026). Members choose from
/// Community's reasons; the Trust case, the decision and the notice to the owner use the platform's.
/// <para>
/// Community takes reports on more than its own posts and comments: guides file theirs here, and
/// the report modal reaches providers, jobs, housing, marketplace listings, events, organisations,
/// polls, members and messages too. Each report is sent to Trust under the content type the owning
/// module's moderation target answers to, so a report filed here and one filed in that module land
/// on the same case.
/// </para>
/// </summary>
public static class CommunityModerationReasons
{
    /// <summary>The platform's reason for one of Community's.</summary>
    public static ModerationReasonCode Map(ReportReasonCode reason) => reason switch
    {
        ReportReasonCode.Misinformation => ModerationReasonCode.FalseOrMisleading,
        ReportReasonCode.Spam => ModerationReasonCode.Spam,

        // The modal's "abusive".
        ReportReasonCode.Harassment => ModerationReasonCode.OffensiveOrHarassment,

        // "Inappropriate content" is the catch-all members reach for when something is distasteful,
        // off-topic or rude without being abuse, and Harassment already carries the abuse. As Other
        // it still reaches the queue on a second report, but it is not one of the serious reasons
        // that let ten reports hide something before anyone has looked: a vague "I don't like this"
        // must not be a lever a group can pull. The moderator gives the real reason when deciding.
        ReportReasonCode.Inappropriate => ModerationReasonCode.Other,

        // Was true and no longer is: not misconduct. The case decides whether it is anything at all.
        ReportReasonCode.Outdated => ModerationReasonCode.Other,

        ReportReasonCode.Copyright => ModerationReasonCode.Copyright,
        ReportReasonCode.PersonalData => ModerationReasonCode.PersonalData,
        _ => ModerationReasonCode.Other
    };

    /// <summary>
    /// The content type a Trust case about this content is filed under: one of
    /// <see cref="ModerationContentTypes"/> where a module moderates it.
    /// <para>
    /// Polls, members and messages have no moderation target yet. Their reports still go to Trust,
    /// under a plain name, and Trust logs and leaves them in Community's own table, where the
    /// Community moderation queue still lists them. A notice is never reportable.
    /// </para>
    /// </summary>
    public static string ContentTypeFor(CommunityContentType type) => type switch
    {
        CommunityContentType.Post => ModerationContentTypes.CommunityPost,
        CommunityContentType.Comment => ModerationContentTypes.CommunityComment,
        CommunityContentType.Guide => ModerationContentTypes.Guide,
        CommunityContentType.Listing => ModerationContentTypes.Provider,
        CommunityContentType.Job => ModerationContentTypes.Job,
        CommunityContentType.Housing => ModerationContentTypes.HousingListing,
        CommunityContentType.Marketplace => ModerationContentTypes.MarketplaceListing,
        CommunityContentType.Event => ModerationContentTypes.Event,
        CommunityContentType.Organization => ModerationContentTypes.Organization,
        CommunityContentType.Poll => "poll",
        CommunityContentType.Member => "member",
        CommunityContentType.Message => "message",
        CommunityContentType.Notice => "notice",
        _ => "other"
    };

    /// <summary>
    /// The Community content type behind a Trust case's content type, so a decision can find the
    /// Community reports it answers. False for a type Community never files under.
    /// </summary>
    public static bool TryParseContentType(string? contentType, out CommunityContentType type)
    {
        foreach (var candidate in Enum.GetValues<CommunityContentType>())
        {
            if (string.Equals(ContentTypeFor(candidate), contentType, StringComparison.OrdinalIgnoreCase))
            {
                type = candidate;
                return true;
            }
        }

        type = default;
        return false;
    }
}
