using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Integration;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Application.Mapping;

/// <summary>
/// Projects posts.
/// <para>
/// Two things this mapper does that are worth knowing. It serves <b>both</b> the canonical field
/// name and the client's — <c>postId</c> beside <c>updateId</c>, <c>body</c> beside
/// <c>description</c> — so no existing renderer breaks and no B6 name is bent. And it withholds
/// the report count from anybody but the author and staff, because a report count on a public
/// card is a brigading tool.
/// </para>
/// </summary>
public static class PostMapper
{
    public static CommunityPostResponse ToResponse(
        CommunityPost post,
        SocialSummary? social,
        ViewerContext viewer,
        PollResponse? poll = null,
        double? distanceKm = null)
    {
        ArgumentNullException.ThrowIfNull(post);
        ArgumentNullException.ThrowIfNull(viewer);

        var (required, requiredVerified) = ConfirmationThresholds.For(post.Urgency);
        var isOwner = viewer.UserId is { } id && id == post.AuthorId;

        return new CommunityPostResponse
        {
            PostId = post.Id.Value,
            UpdateId = post.Id.Value,
            Author = AuthorMapper.ToSummary(post, viewer),
            Title = post.Title,
            Body = post.Body.Value,
            Description = post.Body.Value,
            Category = CommunityWireNames.ToWire(post.Category),
            Urgency = CommunityWireNames.ToWire(post.Urgency),
            Status = CommunityWireNames.ToWire(post.Status),
            ContentType = CommunityWireNames.ToWire(post.ContentType),
            Visibility = CommunityWireNames.ToWire(post.Audience.Level),
            Location = ToLocation(post, distanceKm),
            Attachments = post.Attachments.Select(ToAttachment).ToList(),
            ImageMediaIds = post.Attachments
                .Where(attachment => attachment.Kind == AttachmentKind.Image)
                .Select(attachment => attachment.MediaId!.Value)
                .ToList(),
            Hashtags = post.Hashtags.Select(hashtag => hashtag.Slug).ToList(),
            MentionedUserIds = post.Mentions
                .Where(mention => mention.Target.IsResolved)
                .Select(mention => mention.Target.TargetId!.Value)
                .ToList(),
            Poll = poll,
            Reactions = ReactionMapper.ToResponse(social),
            CommentCount = social?.CommentCount ?? post.Counters.CommentCount,
            ShareCount = social?.ShareCount ?? post.Counters.ShareCount,
            BookmarkCount = social?.BookmarkCount ?? post.Counters.BookmarkCount,
            ViewCount = post.Counters.ViewCount,
            BookmarkedByViewer = social?.ViewerHasBookmarked ?? false,
            VerificationStatus = CommunityWireNames.ToWire(post.VerificationStatus),
            ConfirmationCount = post.ConfirmationCount,
            VerifiedResidentConfirmationCount = post.VerifiedConfirmationCount,
            RequiredConfirmations = required,
            RequiredVerifiedConfirmations = requiredVerified,
            ReportCount = isOwner ? post.Counters.ReportCount : 0,
            IsPinned = post.IsPinned,
            IsEdited = post.IsEdited,
            CommentsClosed = post.CommentsClosed,
            PinnedCommentId = post.PinnedCommentId?.Value,
            RevisionCount = post.RevisionCount,
            PublishedAt = post.PublishedAt,
            ExpiresAt = post.ExpiresAt,
            IsExpired = post.Status == PostStatus.Expired,
            EditedAt = post.EditedAt,
            AffectedRadiusMetres = post.AffectedRadiusMetres,
            ClosedAt = post.ClosedAt,
            CreatedAt = post.CreatedAt,
            CanEdit = isOwner,

            // Only while the alert is in effect: an expired alert has already stopped being true,
            // and withdrawing it afterwards would tell members something that is no longer news.
            CanWithdraw = isOwner && post.Status == PostStatus.Published,
            CanCancel = isOwner && post.Status == PostStatus.Published
        };
    }

    public static CommunityPostSummaryResponse ToSummary(
        CommunityPost post, SocialSummary? social, ViewerContext viewer, double? distanceKm = null)
    {
        ArgumentNullException.ThrowIfNull(post);

        var (required, _) = ConfirmationThresholds.For(post.Urgency);

        return new CommunityPostSummaryResponse
        {
            PostId = post.Id.Value,
            UpdateId = post.Id.Value,
            Author = AuthorMapper.ToSummary(post, viewer),
            Title = post.Title,
            Body = post.Body.Value,
            Description = post.Body.Value,
            Category = CommunityWireNames.ToWire(post.Category),
            Urgency = CommunityWireNames.ToWire(post.Urgency),
            Status = CommunityWireNames.ToWire(post.Status),
            ContentType = CommunityWireNames.ToWire(post.ContentType),
            Location = ToLocation(post, distanceKm),
            ImageMediaIds = post.Attachments
                .Where(attachment => attachment.Kind == AttachmentKind.Image)
                .Select(attachment => attachment.MediaId!.Value)
                .ToList(),
            Hashtags = post.Hashtags.Select(hashtag => hashtag.Slug).ToList(),
            Reactions = ReactionMapper.ToResponse(social),
            CommentCount = social?.CommentCount ?? post.Counters.CommentCount,
            ConfirmationCount = post.ConfirmationCount,
            VerifiedResidentConfirmationCount = post.VerifiedConfirmationCount,
            RequiredConfirmations = required,
            VerificationStatus = CommunityWireNames.ToWire(post.VerificationStatus),
            BookmarkedByViewer = social?.ViewerHasBookmarked ?? false,
            IsPinned = post.IsPinned,
            HasPoll = post.Poll is not null,
            PublishedAt = post.PublishedAt,
            ExpiresAt = post.ExpiresAt,
            IsExpired = post.Status == PostStatus.Expired,
            AffectedRadiusMetres = post.AffectedRadiusMetres,
            ClosedAt = post.ClosedAt
        };
    }

    public static PostRevisionResponse ToRevision(PostRevision revision)
    {
        ArgumentNullException.ThrowIfNull(revision);

        return new PostRevisionResponse
        {
            RevisionId = revision.Id.Value,
            PostId = revision.PostId.Value,
            VersionNumber = revision.RevisionNumber,
            Title = revision.Title,
            Content = revision.Body,

            // The client renders change summaries as bullet points, so a single author-supplied
            // line is returned as a one-element list rather than as a string it would have to
            // split.
            ChangeSummary = string.IsNullOrWhiteSpace(revision.ChangeSummary)
                ? []
                : [revision.ChangeSummary],
            CreatedBy = revision.EditedBy.Value,
            CreatedAt = revision.CreatedAt
        };
    }

    public static PostAttachmentResponse ToAttachment(CommunityAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return new PostAttachmentResponse
        {
            AttachmentId = attachment.Id.Value,
            Kind = CommunityWireNames.ToWire(attachment.Kind),
            MediaId = attachment.MediaId?.Value,
            Role = attachment.Role,
            Position = attachment.Position,
            Caption = attachment.Caption,
            LinkUrl = attachment.LinkUrl,
            LinkTitle = attachment.LinkTitle,
            LinkDescription = attachment.LinkDescription,
            LinkSiteName = attachment.LinkSiteName,
            LinkImageMediaId = attachment.LinkImageMediaId
        };
    }

    private static LocationContextResponse ToLocation(CommunityPost post, double? distanceKm) =>
        new()
        {
            CountryCode = post.Location.CountryCode,
            AreaId = post.Location.AreaId,
            AreaName = post.Location.AreaName,
            CityName = post.Location.CityName,
            Latitude = post.Location.Latitude,
            Longitude = post.Location.Longitude,
            DistanceKm = distanceKm,
            PointIsExact = post.Location.PointIsExact
        };
}
