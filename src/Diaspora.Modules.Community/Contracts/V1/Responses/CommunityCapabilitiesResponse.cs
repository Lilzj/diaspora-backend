namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>
/// What this deployment of Community can do.
/// <para>
/// One route a client reads at startup instead of hardcoding a reaction picker, a feed list, a
/// report modal and a set of limits — every one of which the client currently hardcodes, and
/// three of which it has already got wrong relative to its own types.
/// </para>
/// <para>
/// <c>automatedReviewAvailable</c> is here for the same reason B5 exposes its processing
/// capabilities: a deployment with no review provider registered should be able to say so out
/// loud rather than looking identical to one that has moderation.
/// </para>
/// </summary>
public sealed record CommunityCapabilitiesResponse
{
    public required IReadOnlyList<ReactionDefinitionResponse> Reactions { get; init; }

    public required IReadOnlyList<FeedDescriptorResponse> Feeds { get; init; }

    public required IReadOnlyList<string> Rankings { get; init; }

    public required IReadOnlyList<ReportReasonResponse> ReportReasons { get; init; }

    public required IReadOnlyList<string> VisibilityLevels { get; init; }

    /// <summary>Content types some module has claimed. Social writes on others are refused.</summary>
    public required IReadOnlyList<string> SupportedContentTypes { get; init; }

    public required int MaxPostLength { get; init; }

    public required int MaxCommentLength { get; init; }

    public required int MaxPostAttachments { get; init; }

    public required int MaxPollOptions { get; init; }

    public required int MaxFeedPageSize { get; init; }

    public required int MaxRenderedCommentDepth { get; init; }

    /// <summary>False when nothing is registered. Not the same as a platform with nothing to flag.</summary>
    public required bool AutomatedReviewAvailable { get; init; }
}
