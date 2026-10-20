using System.Globalization;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Domain.Errors;

/// <summary>
/// Every failure the Community domain can express.
/// <para>
/// One file, because the alternative is the same error constructed with two different
/// descriptions in two handlers, and a client that shows different text for the same condition
/// depending on which route produced it.
/// </para>
/// <para>
/// The <see cref="ErrorType"/> on each one is what maps it to an HTTP status through the shared
/// pipeline, so it is a contract decision rather than a stylistic one. Two are worth calling
/// out. Trying to react twice is a <b>Conflict</b>, not a validation failure, because an
/// optimistic client that double-fires must be able to treat it as a no-op. Reading content
/// whose audience excludes the caller is a <b>NotFound</b>, not a Forbidden — telling somebody
/// that a post exists but is not for them leaks its existence, which is exactly what a private
/// audience is for.
/// </para>
/// </summary>
public static class CommunityErrors
{
    public static readonly Error NotAuthenticated = Error.Unauthorized(
        CommunityErrorCodes.NotAuthenticated, "You must be signed in to do that.");

    public static readonly Error Forbidden = Error.Forbidden(
        CommunityErrorCodes.Forbidden, "You are not allowed to do that.");

    public static readonly Error ContentNotFound = Error.NotFound(
        CommunityErrorCodes.ContentNotFound, "That content does not exist.");

    public static readonly Error ContentTypeUnsupported = Error.Validation(
        CommunityErrorCodes.ContentTypeUnsupported,
        "Nothing on this platform owns that kind of content.");

    public static Error EnumUnknown(string name, string value) => Error.Validation(
        CommunityErrorCodes.EnumUnknown,
        string.Format(CultureInfo.InvariantCulture, "'{0}' is not a valid {1}.", value, name));

    // ── Posts ────────────────────────────────────────────────────────────────────────────

    public static readonly Error PostNotFound = Error.NotFound(
        CommunityErrorCodes.PostNotFound, "That post does not exist.");

    public static readonly Error PostBodyEmpty = Error.Validation(
        CommunityErrorCodes.PostBodyEmpty, "A post needs something in it.");

    public static Error PostBodyTooLong(int max) => Error.Validation(
        CommunityErrorCodes.PostBodyTooLong,
        string.Format(CultureInfo.InvariantCulture, "A post can be at most {0} characters.", max));

    public static Error PostTitleTooLong(int max) => Error.Validation(
        CommunityErrorCodes.PostTitleTooLong,
        string.Format(CultureInfo.InvariantCulture, "A title can be at most {0} characters.", max));

    public static readonly Error PostNotEditable = Error.Conflict(
        CommunityErrorCodes.PostNotEditable, "This post can no longer be edited.");

    public static readonly Error PostNotDraft = Error.Conflict(
        CommunityErrorCodes.PostNotDraft, "That post is not a draft.");

    public static readonly Error PostAlreadyPublished = Error.Conflict(
        CommunityErrorCodes.PostAlreadyPublished, "That post has already been published.");

    public static Error PostAttachmentLimit(int max) => Error.Validation(
        CommunityErrorCodes.PostAttachmentLimit,
        string.Format(
            CultureInfo.InvariantCulture, "A post can carry at most {0} attachments.", max));

    public static Error PostHashtagLimit(int max) => Error.Validation(
        CommunityErrorCodes.PostHashtagLimit,
        string.Format(CultureInfo.InvariantCulture, "A post can carry at most {0} hashtags.", max));

    public static Error PostMentionLimit(int max) => Error.Validation(
        CommunityErrorCodes.PostMentionLimit,
        string.Format(CultureInfo.InvariantCulture, "A post can mention at most {0} people.", max));

    public static readonly Error PostNotOwned = Error.Forbidden(
        CommunityErrorCodes.PostNotOwned, "That post is not yours.");

    public static readonly Error PostRemoved = Error.Conflict(
        CommunityErrorCodes.PostRemoved, "That post has been removed.");

    public static readonly Error PostNotOpen = Error.Conflict(
        CommunityErrorCodes.PostNotOpen,
        "Only a published post that is still in effect can be withdrawn, cancelled or amended.");

    public static readonly Error AffectedAreaNeedsPoint = Error.Validation(
        CommunityErrorCodes.AffectedAreaNeedsPoint,
        "An affected area is a distance around a point. Pin the alert on the map first.");

    public static Error AffectedRadiusOutOfRange(int minimum, int maximum) => Error.Validation(
        CommunityErrorCodes.AffectedRadiusOutOfRange,
        string.Format(
            CultureInfo.InvariantCulture,
            "An affected area must be between {0} and {1} metres across its radius.",
            minimum,
            maximum));

    /// <summary>
    /// Refused because the caller is signed in but may not publish at this urgency. A 403, not a
    /// 401: the client offers verification, not sign-in.
    /// </summary>
    public static readonly Error EmergencyNotPermitted = Error.Forbidden(
        CommunityErrorCodes.EmergencyNotPermitted,
        "Only verified members can publish an Emergency alert. Post it as Urgent, or verify your account.");

    // ── Comments ─────────────────────────────────────────────────────────────────────────

    public static readonly Error CommentNotFound = Error.NotFound(
        CommunityErrorCodes.CommentNotFound, "That comment does not exist.");

    public static readonly Error CommentBodyEmpty = Error.Validation(
        CommunityErrorCodes.CommentBodyEmpty, "A comment needs something in it.");

    public static Error CommentBodyTooLong(int max) => Error.Validation(
        CommunityErrorCodes.CommentBodyTooLong,
        string.Format(
            CultureInfo.InvariantCulture, "A comment can be at most {0} characters.", max));

    public static readonly Error CommentNotOwned = Error.Forbidden(
        CommunityErrorCodes.CommentNotOwned, "That comment is not yours.");

    public static readonly Error CommentNotEditable = Error.Conflict(
        CommunityErrorCodes.CommentNotEditable, "This comment can no longer be edited.");

    public static Error CommentDepthExceeded(int max) => Error.Validation(
        CommunityErrorCodes.CommentDepthExceeded,
        string.Format(
            CultureInfo.InvariantCulture, "Replies cannot nest more than {0} deep.", max));

    public static readonly Error CommentParentMismatch = Error.Validation(
        CommunityErrorCodes.CommentParentMismatch,
        "That reply belongs to a different piece of content.");

    public static readonly Error CommentsClosed = Error.Conflict(
        CommunityErrorCodes.CommentsClosed, "Comments are closed on this content.");

    // ── Reactions ────────────────────────────────────────────────────────────────────────

    public static Error ReactionUnknown(string key) => Error.Validation(
        CommunityErrorCodes.ReactionUnknown,
        string.Format(CultureInfo.InvariantCulture, "'{0}' is not a reaction anyone offers.", key));

    public static readonly Error ReactionDuplicate = Error.Conflict(
        CommunityErrorCodes.ReactionDuplicate, "You have already reacted that way.");

    public static readonly Error ReactionNotFound = Error.NotFound(
        CommunityErrorCodes.ReactionNotFound, "You have not reacted to that.");

    public static readonly Error ReactionSelfCorroboration = Error.Conflict(
        CommunityErrorCodes.ReactionSelfCorroboration,
        "You cannot confirm your own post.");

    // ── Shares, bookmarks, follows ───────────────────────────────────────────────────────

    public static readonly Error ShareInvalidTarget = Error.Validation(
        CommunityErrorCodes.ShareInvalidTarget, "That share has no valid destination.");

    public static readonly Error BookmarkDuplicate = Error.Conflict(
        CommunityErrorCodes.BookmarkDuplicate, "You have already saved that.");

    public static readonly Error BookmarkNotFound = Error.NotFound(
        CommunityErrorCodes.BookmarkNotFound, "That is not saved.");

    public static readonly Error BookmarkFolderNotFound = Error.NotFound(
        CommunityErrorCodes.BookmarkFolderNotFound, "That folder does not exist.");

    public static Error BookmarkFolderLimit(int max) => Error.Validation(
        CommunityErrorCodes.BookmarkFolderLimit,
        string.Format(CultureInfo.InvariantCulture, "You can have at most {0} folders.", max));

    public static readonly Error BookmarkFolderNameTaken = Error.Conflict(
        CommunityErrorCodes.BookmarkFolderNameTaken, "You already have a folder with that name.");

    public static readonly Error FollowSelf = Error.Validation(
        CommunityErrorCodes.FollowSelf, "You cannot follow yourself.");

    public static readonly Error FollowDuplicate = Error.Conflict(
        CommunityErrorCodes.FollowDuplicate, "You already follow that.");

    public static readonly Error FollowNotFound = Error.NotFound(
        CommunityErrorCodes.FollowNotFound, "You do not follow that.");

    // ── Hashtags ─────────────────────────────────────────────────────────────────────────

    public static Error HashtagTooShort(int min) => Error.Validation(
        CommunityErrorCodes.HashtagTooShort,
        string.Format(
            CultureInfo.InvariantCulture, "A hashtag needs at least {0} characters.", min));

    public static Error HashtagTooLong(int max) => Error.Validation(
        CommunityErrorCodes.HashtagTooLong,
        string.Format(
            CultureInfo.InvariantCulture, "A hashtag can be at most {0} characters.", max));

    public static readonly Error HashtagBlocked = Error.Validation(
        CommunityErrorCodes.HashtagBlocked, "That hashtag is not available.");

    // ── Polls ────────────────────────────────────────────────────────────────────────────

    public static readonly Error PollNotFound = Error.NotFound(
        CommunityErrorCodes.PollNotFound, "That poll does not exist.");

    public static readonly Error PollClosed = Error.Conflict(
        CommunityErrorCodes.PollClosed, "That poll has closed.");

    public static readonly Error PollOptionNotFound = Error.NotFound(
        CommunityErrorCodes.PollOptionNotFound, "That is not an option on this poll.");

    public static Error PollOptionCount(int min, int max) => Error.Validation(
        CommunityErrorCodes.PollOptionCount,
        string.Format(
            CultureInfo.InvariantCulture, "A poll needs between {0} and {1} options.", min, max));

    public static readonly Error PollAlreadyVoted = Error.Conflict(
        CommunityErrorCodes.PollAlreadyVoted, "You have already voted in this poll.");

    public static readonly Error PollSingleChoice = Error.Validation(
        CommunityErrorCodes.PollSingleChoice, "This poll takes one answer.");

    public static readonly Error PollResultsHidden = Error.Forbidden(
        CommunityErrorCodes.PollResultsHidden, "Vote to see the results.");

    // ── Moderation ───────────────────────────────────────────────────────────────────────

    public static readonly Error ReportDuplicate = Error.Conflict(
        CommunityErrorCodes.ReportDuplicate, "You have already reported that.");

    public static readonly Error ReportNotFound = Error.NotFound(
        CommunityErrorCodes.ReportNotFound, "That report does not exist.");

    public static readonly Error ReportSelfContent = Error.Validation(
        CommunityErrorCodes.ReportSelfContent, "You cannot report your own content.");

    public static readonly Error ReportEvidenceRequired = Error.Validation(
        CommunityErrorCodes.ReportEvidenceRequired,
        "This reason needs a link to the conflicting content.");

    public static readonly Error ContentNotReportable = Error.Validation(
        CommunityErrorCodes.ContentNotReportable,
        "Official notices are published by Diaspora and cannot be reported.");

    public static readonly Error ReportAlreadyResolved = Error.Conflict(
        CommunityErrorCodes.ReportAlreadyResolved, "That report has already been decided.");

    public static readonly Error ModerationDecisionInvalid = Error.Validation(
        CommunityErrorCodes.ModerationDecisionInvalid,
        "That decision does not apply to this content.");

    public static readonly Error ModerationNotPermitted = Error.Forbidden(
        CommunityErrorCodes.ModerationNotPermitted, "You cannot moderate that.");

    public static readonly Error ReportEscalated = Error.Conflict(
        CommunityErrorCodes.ReportEscalated,
        "That report has been escalated to the platform's moderators, who will decide it.");

    public static readonly Error ReportStatusUnknown = Error.Validation(
        CommunityErrorCodes.ReportStatusUnknown,
        "Choose Open, UnderReview, Actioned, Dismissed or Escalated.");

    public static readonly Error ModerationDecisionUnknown = Error.Validation(
        CommunityErrorCodes.ModerationDecisionUnknown,
        "Choose dismiss, remove-content, warn or escalate.");

    /// <summary>
    /// Refused rather than recorded. The platform has no way to deliver a warning to an author, and a
    /// queue that marked a report "warned" when nobody was told anything would be a decision that
    /// only looks taken.
    /// </summary>
    public static readonly Error ModerationWarnUnavailable = Error.Validation(
        CommunityErrorCodes.ModerationWarnUnavailable,
        "Warnings cannot be sent yet. Remove the content, dismiss the report, or escalate it.");

    // ── Visibility and feeds ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Deliberately a <b>NotFound</b>. Answering "that exists but is not for you" tells a caller
    /// the content exists, which is precisely what a narrow audience is meant to withhold.
    /// </summary>
    public static readonly Error VisibilityDenied = Error.NotFound(
        CommunityErrorCodes.VisibilityDenied, "That content does not exist.");

    public static readonly Error VisibilityRuleNotFound = Error.NotFound(
        CommunityErrorCodes.VisibilityRuleNotFound, "That audience rule does not exist.");

    public static readonly Error VisibilityWidened = Error.Conflict(
        CommunityErrorCodes.VisibilityWidened,
        "A visibility evaluator tried to widen an audience.");

    public static Error FeedUnknown(string key) => Error.NotFound(
        CommunityErrorCodes.FeedUnknown,
        string.Format(CultureInfo.InvariantCulture, "There is no '{0}' feed.", key));

    public static readonly Error FeedScopeRequired = Error.Validation(
        CommunityErrorCodes.FeedScopeRequired, "That feed needs a place to look.");

    public static Error RankingUnknown(string key) => Error.Validation(
        CommunityErrorCodes.RankingUnknown,
        string.Format(CultureInfo.InvariantCulture, "There is no '{0}' ranking.", key));
}
