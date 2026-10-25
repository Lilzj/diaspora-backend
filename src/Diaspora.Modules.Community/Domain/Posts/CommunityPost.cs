using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Polls;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Domain.Posts;

/// <summary>
/// A piece of member-authored community content.
/// <para>
/// The aggregate the whole module is arranged around. It owns its attachments, its hashtag and
/// mention associations, its revision history and its poll — everything whose lifetime is
/// exactly the post's lifetime and whose consistency has to be enforced in one transaction.
/// </para>
/// <para>
/// <b>What it deliberately does not own</b> is as important. Comments, reactions, bookmarks,
/// shares and reports are separate aggregates keyed by a
/// <see cref="ContentReference"/>, not children of this one. Three reasons, in ascending order
/// of importance: a thread with ten thousand comments cannot be loaded to add the ten-thousand-
/// and-first; two members reacting at once would contend on the post row; and — the real reason
/// — a comment that belonged to a post could never belong to a guide, and the entire premise of
/// B6 is that it can.
/// </para>
/// <para>
/// <b>Counters are denormalised onto the post</b> and are never load-bearing for an invariant.
/// See <see cref="EngagementCounters"/> for why that trade is safe.
/// </para>
/// </summary>
public sealed class CommunityPost
    : AggregateRoot<CommunityPostId>, IAuditableEntity, ISoftDeletable
{
    private readonly List<CommunityAttachment> _attachments = [];
    private readonly List<PostHashtag> _hashtags = [];
    private readonly List<PostMention> _mentions = [];
    private readonly List<PostRevision> _revisions = [];

    private CommunityPost(
        CommunityPostId id,
        UserId authorId,
        PublishingIdentity publisher,
        PostBody body,
        string? title,
        PostCategory category,
        UrgencyLevel urgency,
        ContentAudience audience,
        PostLocationContext location,
        DateTimeOffset createdAt,
        int lifetimeHours) : base(id)
    {
        AuthorId = authorId;
        Publisher = publisher;
        Body = body;
        Title = title;
        Category = category;
        Urgency = urgency;
        Audience = audience;
        Location = location;
        Status = PostStatus.Draft;
        ContentType = PostContentType.Text;
        VerificationStatus = PostVerificationStatus.Unverified;
        Counters = EngagementCounters.Zero;
        LifetimeHours = lifetimeHours;
        CreatedAt = createdAt;
    }

    private CommunityPost() { }

    public UserId AuthorId { get; private set; } = default!;

    /// <summary>Who the post is attributed to. The author is who is accountable for it.</summary>
    public PublishingIdentity Publisher { get; private set; } = default!;

    public PostBody Body { get; private set; } = default!;

    /// <summary>
    /// Optional, and stored as a plain string rather than an owned value object.
    /// <see cref="PostTitle"/> owns the rule and is applied on the way in; storing it as an
    /// optional owned reference whose only property is nullable is the EF Core shape that
    /// cannot tell an absent instance from an empty one.
    /// </summary>
    public string? Title { get; private set; }

    public PostCategory Category { get; private set; }

    public UrgencyLevel Urgency { get; private set; }

    public PostStatus Status { get; private set; }

    public PostContentType ContentType { get; private set; }

    public ContentAudience Audience { get; private set; } = ContentAudience.Public;

    public PostLocationContext Location { get; private set; } = PostLocationContext.None;

    public EngagementCounters Counters { get; private set; } = EngagementCounters.Zero;

    public PostVerificationStatus VerificationStatus { get; private set; }

    /// <summary>Corroborating reactions received. Kept beside the counters for threshold checks.</summary>
    public int ConfirmationCount { get; private set; }

    /// <summary>Corroborations from members Trust has verified as residents of the country.</summary>
    public int VerifiedConfirmationCount { get; private set; }

    public int DisputeCount { get; private set; }

    public bool IsPinned { get; private set; }

    /// <summary>The comment an author or moderator pinned to the top of the thread.</summary>
    public CommentId? PinnedCommentId { get; private set; }

    public bool CommentsClosed { get; private set; }

    /// <summary>Hours from publication to expiry. Resolved from the category unless declared.</summary>
    public int LifetimeHours { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? ExpiresAt { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    /// <summary>
    /// How far around its point the post applies, in metres, when its author said. Null is a point,
    /// which is every post written before 16 Sep 2026.
    /// <para>
    /// A road closure reported at one junction covers the road only when its author gives it a
    /// radius. The map draws exactly this circle, and nothing the client invents: drawing 500 m around
    /// a report nobody sized would be the platform deciding the scope of somebody's emergency.
    /// </para>
    /// </summary>
    public int? AffectedRadiusMetres { get; private set; }

    /// <summary>When its author withdrew or cancelled it. Null while it is open.</summary>
    public DateTimeOffset? ClosedAt { get; private set; }

    /// <summary>True once an edit happened outside the silent window. The client renders this.</summary>
    public bool IsEdited { get; private set; }

    public int RevisionCount { get; private set; }

    public Poll? Poll { get; private set; }

    public IReadOnlyList<CommunityAttachment> Attachments =>
        _attachments.OrderBy(attachment => attachment.Position).ToList().AsReadOnly();

    public IReadOnlyList<PostHashtag> Hashtags => _hashtags.AsReadOnly();

    public IReadOnlyList<PostMention> Mentions => _mentions.AsReadOnly();

    public IReadOnlyList<PostRevision> Revisions =>
        _revisions.OrderByDescending(revision => revision.RevisionNumber).ToList().AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public string? DeletedBy { get; private set; }

    /// <summary>The reference every comment, reaction, report and bookmark points at.</summary>
    public ContentReference Reference => ContentReference.To(CommunityContentType.Post, Id);

    public bool IsPublished => Status is PostStatus.Published;

    public bool IsVisibleToOthers =>
        Status is PostStatus.Published
            or PostStatus.Expired
            or PostStatus.Withdrawn
            or PostStatus.Cancelled
        && !IsDeleted;

    /// <summary>Whether its author has withdrawn or cancelled it.</summary>
    public bool IsClosed => Status is PostStatus.Withdrawn or PostStatus.Cancelled;

    public static Result<CommunityPost> Draft(
        UserId authorId,
        PublishingIdentity publisher,
        PostBody body,
        PostTitle? title,
        PostCategory category,
        UrgencyLevel urgency,
        ContentAudience audience,
        PostLocationContext location,
        DateTimeOffset now,
        int? lifetimeHours = null,
        int? affectedRadiusMetres = null)
    {
        ArgumentNullException.ThrowIfNull(authorId);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(audience);
        ArgumentNullException.ThrowIfNull(location);

        var post = new CommunityPost(
            CommunityPostId.New(),
            authorId,
            publisher,
            body,
            title?.Value,
            category,
            urgency,
            audience,
            location,
            now,
            PostExpiryPolicy.Resolve(category, lifetimeHours));

        var area = post.ApplyAffectedRadius(affectedRadiusMetres);

        return area.IsFailure ? Result.Failure<CommunityPost>(area.Error) : Result.Success(post);
    }

    /// <summary>
    /// Makes the post visible and starts its clock.
    /// <para>
    /// The content type is resolved here rather than tracked as attachments arrive, because a
    /// draft that gains and loses an image would otherwise leave a stale type behind. Resolving
    /// once, at the moment the post becomes real, means the stored type always matches what was
    /// actually published.
    /// </para>
    /// </summary>
    public Result Publish(DateTimeOffset now)
    {
        if (Status is PostStatus.Published)
        {
            return Result.Failure(CommunityErrors.PostAlreadyPublished);
        }

        if (Status is PostStatus.Removed or PostStatus.Deleted)
        {
            return Result.Failure(CommunityErrors.PostRemoved);
        }

        Status = PostStatus.Published;
        ContentType = ResolveContentType();
        PublishedAt = now;
        ExpiresAt = now.AddHours(LifetimeHours);
        RevisionCount = 1;

        _revisions.Add(PostRevision.Capture(
            Id, 1, Title, Body.Value, AuthorId, null, now));

        RaiseDomainEvent(new PostPublishedDomainEvent
        {
            PostId = Id,
            AuthorId = AuthorId,
            Category = Category,
            Urgency = Urgency,
            Visibility = Audience.Level,
            CountryCode = Location.CountryCode,
            AreaId = Location.AreaId,
            OrganizationId = Publisher.OrganizationId
        });

        return Result.Success();
    }

    /// <summary>
    /// Rewrites the body, capturing what was there first.
    /// <para>
    /// An edit inside the silent window does not mark the post as edited. Five minutes to fix a
    /// typo without a permanent badge is the convention every platform has converged on, and the
    /// alternative — marking every post edited — makes the marker meaningless, which removes the
    /// only signal a reader has that a post said something different when it was replied to.
    /// </para>
    /// </summary>
    public Result Edit(
        PostBody body, PostTitle? title, UserId editedBy, string? changeSummary, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(editedBy);

        if (Status is PostStatus.Removed or PostStatus.Deleted)
        {
            return Result.Failure(CommunityErrors.PostRemoved);
        }

        // A withdrawn or cancelled alert is a record of what was said and then taken back.
        // Rewriting it afterwards would change what members acted on.
        if (Status is PostStatus.Archived || IsClosed)
        {
            return Result.Failure(CommunityErrors.PostNotEditable);
        }

        _revisions.Add(PostRevision.Capture(
            Id, RevisionCount + 1, Title, Body.Value, editedBy, changeSummary, now));

        RevisionCount++;
        PruneRevisions();

        Body = body;
        Title = title?.Value;
        ContentType = ResolveContentType();
        EditedAt = now;

        var withinSilentWindow =
            PublishedAt is { } published && now - published <= CommunityLimits.SilentEditWindow;

        if (!withinSilentWindow && Status is PostStatus.Published)
        {
            IsEdited = true;
        }

        RaiseDomainEvent(new PostEditedDomainEvent
        {
            PostId = Id,
            EditedBy = editedBy,
            RevisionNumber = RevisionCount,
            IsMarkedEdited = IsEdited
        });

        return Result.Success();
    }

    public Result Attach(CommunityAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        if (_attachments.Count >= CommunityLimits.MaxPostAttachments)
        {
            return Result.Failure(
                CommunityErrors.PostAttachmentLimit(CommunityLimits.MaxPostAttachments));
        }

        attachment.MoveTo(_attachments.Count);
        _attachments.Add(attachment);
        ContentType = ResolveContentType();

        return Result.Success();
    }

    public Result Detach(CommunityAttachmentId attachmentId)
    {
        ArgumentNullException.ThrowIfNull(attachmentId);

        var attachment = _attachments.FirstOrDefault(candidate => candidate.Id == attachmentId);

        if (attachment is null)
        {
            return Result.Failure(CommunityErrors.ContentNotFound);
        }

        _attachments.Remove(attachment);
        Resequence();
        ContentType = ResolveContentType();

        return Result.Success();
    }

    public Result AddHashtag(HashtagId hashtagId, string slug)
    {
        ArgumentNullException.ThrowIfNull(hashtagId);

        if (_hashtags.Exists(existing =>
                string.Equals(existing.Slug, slug, StringComparison.Ordinal)))
        {
            return Result.Success();
        }

        if (_hashtags.Count >= CommunityLimits.MaxHashtagsPerPost)
        {
            return Result.Failure(
                CommunityErrors.PostHashtagLimit(CommunityLimits.MaxHashtagsPerPost));
        }

        _hashtags.Add(PostHashtag.Create(hashtagId, slug, _hashtags.Count));

        return Result.Success();
    }

    public Result AddMention(MentionTarget target, int startOffset, int length)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_mentions.Count >= CommunityLimits.MaxMentionsPerPost)
        {
            return Result.Failure(
                CommunityErrors.PostMentionLimit(CommunityLimits.MaxMentionsPerPost));
        }

        _mentions.Add(PostMention.Create(target, startOffset, length));

        return Result.Success();
    }

    /// <summary>Drops every hashtag and mention, so a re-edited body can be re-extracted.</summary>
    public void ClearExtractions()
    {
        _hashtags.Clear();
        _mentions.Clear();
    }

    public Result AttachPoll(Poll poll)
    {
        ArgumentNullException.ThrowIfNull(poll);

        Poll = poll;
        ContentType = ResolveContentType();

        return Result.Success();
    }

    /// <summary>
    /// Records a corroborating reaction and re-evaluates the verification threshold.
    /// <para>
    /// Called by the reaction handler rather than computed on read, because the transition
    /// across a threshold is an event other modules subscribe to, and an event can only be
    /// raised at the moment the transition happens.
    /// </para>
    /// </summary>
    public void RecordCorroboration(bool byVerifiedResident, int delta)
    {
        ConfirmationCount = Math.Max(0, ConfirmationCount + delta);

        if (byVerifiedResident)
        {
            VerifiedConfirmationCount = Math.Max(0, VerifiedConfirmationCount + delta);
        }

        ReevaluateVerification();
    }

    public void RecordDispute(int delta)
    {
        DisputeCount = Math.Max(0, DisputeCount + delta);

        ReevaluateVerification();
    }

    public void ApplyEngagement(Func<EngagementCounters, EngagementCounters> change)
    {
        ArgumentNullException.ThrowIfNull(change);

        Counters = change(Counters);
    }

    public void Pin(bool pinned) => IsPinned = pinned;

    public void PinComment(CommentId? commentId) => PinnedCommentId = commentId;

    public void SetCommentsClosed(bool closed) => CommentsClosed = closed;

    public Result Expire(DateTimeOffset now)
    {
        if (Status is not PostStatus.Published)
        {
            return Result.Success();
        }

        Status = PostStatus.Expired;

        RaiseDomainEvent(new PostExpiredDomainEvent { PostId = Id, ExpiredAt = now });

        return Result.Success();
    }

    /// <summary>
    /// Retracts the post because the report was wrong. Its author only, and only while it is in
    /// effect; see <see cref="PostStatus.Withdrawn"/>.
    /// </summary>
    public Result Withdraw(UserId by, DateTimeOffset now) => Close(PostStatus.Withdrawn, by, now);

    /// <summary>
    /// Calls the post off because what it reported is over or will not happen. Its author only, and
    /// only while it is in effect; see <see cref="PostStatus.Cancelled"/>.
    /// </summary>
    public Result Cancel(UserId by, DateTimeOffset now) => Close(PostStatus.Cancelled, by, now);

    /// <summary>
    /// Changes how urgent a published post is, how far it reaches, or both.
    /// <para>
    /// <paramref name="affectedRadiusMetres"/> follows the wire: null leaves the area as it is, zero
    /// removes it, and anything else replaces it. Who may raise a post to Emergency is decided by the
    /// handler, which holds the caller's permissions; the aggregate holds only what an amendment is.
    /// </para>
    /// <para>
    /// Marked edited on the same rule as <see cref="Edit"/>: silently inside the first five minutes,
    /// publicly after. A member who planned around "Urgent, 2 km" and now reads "Emergency, 10 km"
    /// deserves to see that it changed.
    /// </para>
    /// </summary>
    public Result AmendAlert(
        UrgencyLevel? urgency, int? affectedRadiusMetres, UserId by, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(by);

        if (by != AuthorId)
        {
            return Result.Failure(CommunityErrors.PostNotOwned);
        }

        if (Status is PostStatus.Removed or PostStatus.Deleted)
        {
            return Result.Failure(CommunityErrors.PostRemoved);
        }

        if (Status is not PostStatus.Published)
        {
            return Result.Failure(CommunityErrors.PostNotOpen);
        }

        var nextUrgency = urgency ?? Urgency;
        var nextRadius = affectedRadiusMetres switch
        {
            null => AffectedRadiusMetres,
            0 => null,
            var radius => radius
        };

        if (nextUrgency == Urgency && nextRadius == AffectedRadiusMetres)
        {
            return Result.Success();
        }

        var area = ApplyAffectedRadius(nextRadius);

        if (area.IsFailure)
        {
            return area;
        }

        Urgency = nextUrgency;
        EditedAt = now;

        if (PublishedAt is { } published && now - published > CommunityLimits.SilentEditWindow)
        {
            IsEdited = true;
        }

        RaiseDomainEvent(new PostAlertAmendedDomainEvent
        {
            PostId = Id,
            Urgency = Urgency,
            AffectedRadiusMetres = AffectedRadiusMetres,
            AmendedAt = now
        });

        return Result.Success();
    }

    private Result Close(PostStatus closing, UserId by, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(by);

        if (by != AuthorId)
        {
            return Result.Failure(CommunityErrors.PostNotOwned);
        }

        if (Status is PostStatus.Removed or PostStatus.Deleted)
        {
            return Result.Failure(CommunityErrors.PostRemoved);
        }

        if (Status is not PostStatus.Published)
        {
            return Result.Failure(CommunityErrors.PostNotOpen);
        }

        Status = closing;
        ClosedAt = now;

        RaiseDomainEvent(closing == PostStatus.Withdrawn
            ? new PostWithdrawnDomainEvent { PostId = Id, AuthorId = AuthorId, ClosedAt = now }
            : new PostCancelledDomainEvent { PostId = Id, AuthorId = AuthorId, ClosedAt = now });

        return Result.Success();
    }

    /// <summary>
    /// A radius needs a point its author chose to be around, and a size a phone can draw. A centroid
    /// does not count: see <see cref="PostLocationContext.PointIsExact"/>.
    /// </summary>
    private Result ApplyAffectedRadius(int? radiusMetres)
    {
        if (radiusMetres is null)
        {
            AffectedRadiusMetres = null;

            return Result.Success();
        }

        if (!Location.HasCoordinates || !Location.PointIsExact)
        {
            return Result.Failure(CommunityErrors.AffectedAreaNeedsPoint);
        }

        if (radiusMetres < CommunityLimits.MinAffectedRadiusMetres
            || radiusMetres > CommunityLimits.MaxAffectedRadiusMetres)
        {
            return Result.Failure(CommunityErrors.AffectedRadiusOutOfRange(
                CommunityLimits.MinAffectedRadiusMetres, CommunityLimits.MaxAffectedRadiusMetres));
        }

        AffectedRadiusMetres = radiusMetres;

        return Result.Success();
    }

    public Result Archive()
    {
        if (Status is PostStatus.Removed or PostStatus.Deleted)
        {
            return Result.Failure(CommunityErrors.PostRemoved);
        }

        Status = PostStatus.Archived;

        return Result.Success();
    }

    /// <summary>Applies a moderator decision. Recorded here; the decision row is elsewhere.</summary>
    public Result ApplyModeration(ModerationActionType action)
    {
        switch (action)
        {
            case ModerationActionType.Hide:
                Status = PostStatus.Hidden;
                break;

            case ModerationActionType.Reject:
                Status = PostStatus.Removed;
                RaiseDomainEvent(new PostDeletedDomainEvent
                {
                    PostId = Id, AuthorId = AuthorId, ByModerator = true
                });
                break;

            case ModerationActionType.Archive:
                Status = PostStatus.Archived;
                break;

            case ModerationActionType.Restore:
            case ModerationActionType.Approve:
                Status = PublishedAt is null ? PostStatus.Draft : PostStatus.Published;
                break;

            case ModerationActionType.Pin:
                IsPinned = true;
                break;

            case ModerationActionType.Unpin:
                IsPinned = false;
                break;

            case ModerationActionType.Suspend:
                // Recorded and published; acted on by Identity. Nothing changes on the post.
                break;

            default:
                return Result.Failure(CommunityErrors.ModerationDecisionInvalid);
        }

        return Result.Success();
    }

    /// <summary>
    /// Marks the post deleted by its author.
    /// <para>
    /// Sets the soft-delete fields directly rather than relying on the repository calling
    /// <c>Remove</c>. That was a workaround in B5 for a defect B1.0.1 has since fixed, and it
    /// stays the deliberate shape here for a different reason: deleting a post has to raise an
    /// event and set a status, and a delete that happens in the change tracker cannot do either.
    /// </para>
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        Status = PostStatus.Deleted;
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;

        RaiseDomainEvent(new PostDeletedDomainEvent
        {
            PostId = Id, AuthorId = AuthorId, ByModerator = false
        });
    }

    /// <summary>
    /// The one place the post decides what kind of thing it is.
    /// <para>
    /// Ordered by richness so that the primary presentation wins: a poll with an image is a
    /// poll, and a video with a link is a video. Two clients cannot disagree about the answer
    /// because neither of them computes it.
    /// </para>
    /// </summary>
    private PostContentType ResolveContentType()
    {
        if (Poll is not null)
        {
            return PostContentType.Poll;
        }

        if (_attachments.Exists(a => a.Kind == AttachmentKind.Video))
        {
            return PostContentType.Video;
        }

        if (_attachments.Exists(a => a.Kind == AttachmentKind.Image))
        {
            return PostContentType.Image;
        }

        if (_attachments.Exists(a => a.Kind == AttachmentKind.Document))
        {
            return PostContentType.Document;
        }

        return _attachments.Exists(a => a.Kind == AttachmentKind.Link)
            ? PostContentType.Link
            : PostContentType.Text;
    }

    private void ReevaluateVerification()
    {
        var previous = VerificationStatus;
        var (total, verified) = ConfirmationThresholds.For(Urgency);

        var corroborated =
            ConfirmationCount >= total && VerifiedConfirmationCount >= verified;

        var disputed =
            DisputeCount >= ConfirmationThresholds.DisputeThreshold(ConfirmationCount);

        // Dispute wins over corroboration. A post that is both heavily confirmed and heavily
        // disputed is exactly the case a reader needs warned about, and showing it as verified
        // would be the most damaging possible reading of the same numbers.
        VerificationStatus = disputed
            ? PostVerificationStatus.Disputed
            : corroborated
                ? PostVerificationStatus.CommunityVerified
                : PostVerificationStatus.Unverified;

        if (VerificationStatus == previous)
        {
            return;
        }

        RaiseDomainEvent(new PostVerificationChangedDomainEvent
        {
            PostId = Id,
            Previous = previous,
            Current = VerificationStatus,
            Confirmations = ConfirmationCount,
            VerifiedConfirmations = VerifiedConfirmationCount,
            AuthorId = AuthorId,
            CountryCode = Location.CountryCode
        });
    }

    private void PruneRevisions()
    {
        while (_revisions.Count > CommunityLimits.MaxRevisionsRetained)
        {
            var oldest = _revisions.MinBy(revision => revision.RevisionNumber);

            if (oldest is null)
            {
                return;
            }

            _revisions.Remove(oldest);
        }
    }

    private void Resequence()
    {
        var ordered = _attachments.OrderBy(attachment => attachment.Position).ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].MoveTo(index);
        }
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    /// <summary>
    /// Undoes a soft delete. Restores the post to draft when it never published, and to
    /// published when it did — the state it can meaningfully return to, rather than a status
    /// that would put an unpublished post into a feed.
    /// </summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        Status = PublishedAt is null ? PostStatus.Draft : PostStatus.Published;
    }
}
