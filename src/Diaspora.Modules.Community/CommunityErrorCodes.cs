namespace Diaspora.Modules.Community;

/// <summary>
/// The stable, machine-readable error codes the module emits.
/// <para>
/// A client branches on these. <c>community.post.body-too-long</c> renders a character counter
/// in red; <c>community.reaction.duplicate</c> is an optimistic-update no-op rather than an
/// error toast. That is why they are constants in one file with a frozen prefix rather than
/// strings at throw sites: a renamed code silently turns a handled case into a generic failure
/// on every client that shipped before the rename.
/// </para>
/// <para>
/// Namespaced by capability — <c>community.post.*</c>, <c>community.comment.*</c> — so a client
/// can prefix-match a whole family when it does not care which member of it fired.
/// </para>
/// </summary>
public static class CommunityErrorCodes
{
    public const string NotAuthenticated = "community.not-authenticated";
    public const string Forbidden = "community.forbidden";
    public const string ContentNotFound = "community.content.not-found";
    public const string ContentTypeUnsupported = "community.content.type-unsupported";
    public const string EnumUnknown = "community.enum.unknown";

    public const string PostNotFound = "community.post.not-found";
    public const string PostBodyEmpty = "community.post.body-empty";
    public const string PostBodyTooLong = "community.post.body-too-long";
    public const string PostTitleTooLong = "community.post.title-too-long";
    public const string PostNotEditable = "community.post.not-editable";
    public const string PostNotDraft = "community.post.not-draft";
    public const string PostAlreadyPublished = "community.post.already-published";
    public const string PostAttachmentLimit = "community.post.attachment-limit";
    public const string PostHashtagLimit = "community.post.hashtag-limit";
    public const string PostMentionLimit = "community.post.mention-limit";
    public const string PostNotOwned = "community.post.not-owned";
    public const string PostRemoved = "community.post.removed";
    public const string PostNotOpen = "community.post.not-open";
    public const string AffectedAreaNeedsPoint = "community.post.affected-area.needs-point";
    public const string AffectedRadiusOutOfRange = "community.post.affected-area.out-of-range";
    public const string EmergencyNotPermitted = "community.post.emergency.not-permitted";
    public const string PostPublisherForbidden = "community.post.publisher-forbidden";

    public const string CommentNotFound = "community.comment.not-found";
    public const string CommentBodyEmpty = "community.comment.body-empty";
    public const string CommentBodyTooLong = "community.comment.body-too-long";
    public const string CommentNotOwned = "community.comment.not-owned";
    public const string CommentNotEditable = "community.comment.not-editable";
    public const string CommentDepthExceeded = "community.comment.depth-exceeded";
    public const string CommentParentMismatch = "community.comment.parent-mismatch";
    public const string CommentsClosed = "community.comment.closed";

    public const string ReactionUnknown = "community.reaction.unknown-kind";
    public const string ReactionDuplicate = "community.reaction.duplicate";
    public const string ReactionNotFound = "community.reaction.not-found";
    public const string ReactionSelfCorroboration = "community.reaction.self-corroboration";

    public const string ShareInvalidTarget = "community.share.invalid-target";

    public const string BookmarkDuplicate = "community.bookmark.duplicate";
    public const string BookmarkNotFound = "community.bookmark.not-found";
    public const string BookmarkFolderNotFound = "community.bookmark.folder-not-found";
    public const string BookmarkFolderLimit = "community.bookmark.folder-limit";
    public const string BookmarkFolderNameTaken = "community.bookmark.folder-name-taken";

    public const string FollowSelf = "community.follow.self";
    public const string FollowDuplicate = "community.follow.duplicate";
    public const string FollowNotFound = "community.follow.not-found";

    public const string HashtagTooShort = "community.hashtag.too-short";
    public const string HashtagTooLong = "community.hashtag.too-long";
    public const string HashtagBlocked = "community.hashtag.blocked";

    public const string PollNotFound = "community.poll.not-found";
    public const string PollClosed = "community.poll.closed";
    public const string PollOptionNotFound = "community.poll.option-not-found";
    public const string PollOptionCount = "community.poll.option-count";
    public const string PollAlreadyVoted = "community.poll.already-voted";
    public const string PollSingleChoice = "community.poll.single-choice";
    public const string PollResultsHidden = "community.poll.results-hidden";

    public const string ReportDuplicate = "community.report.duplicate";
    public const string ReportNotFound = "community.report.not-found";
    public const string ReportSelfContent = "community.report.self-content";
    public const string ReportEvidenceRequired = "community.report.evidence-required";
    public const string ContentNotReportable = "community.report.not-reportable";
    public const string ReportAlreadyResolved = "community.report.already-resolved";

    public const string ModerationDecisionInvalid = "community.moderation.invalid-decision";
    public const string ModerationNotPermitted = "community.moderation.not-permitted";

    // The per-community queue, 21 Oct 2026. See PublisherReportQueue.
    public const string ReportEscalated = "community.report.escalated";
    public const string ReportStatusUnknown = "community.report.status-unknown";
    public const string ModerationDecisionUnknown = "community.moderation.decision-unknown";
    public const string ModerationWarnUnavailable = "community.moderation.warn-unavailable";

    public const string VisibilityDenied = "community.visibility.denied";
    public const string VisibilityRuleNotFound = "community.visibility.rule-not-found";
    public const string VisibilityWidened = "community.visibility.widened";

    public const string FeedUnknown = "community.feed.unknown";
    public const string FeedScopeRequired = "community.feed.scope-required";
    public const string RankingUnknown = "community.ranking.unknown";
}
