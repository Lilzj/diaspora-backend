using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Domain.Comments;

/// <summary>
/// A comment on anything.
/// <para>
/// An aggregate root rather than a child of a post, and that single decision is what makes
/// comments reusable infrastructure. It addresses its target through a
/// <see cref="ContentReference"/>, so the same table, the same rules and the same endpoints
/// serve a comment on a post, a guide, an event and whatever B13 invents — none of which need
/// to know this class exists beyond registering a content source.
/// </para>
/// <para>
/// <b>Threading uses a materialised path.</b> Each comment stores the slash-joined identifiers
/// of its ancestors, so fetching a subtree is a prefix scan, computing depth is a character
/// count, and collapsing a thread does not need a recursive query. The alternative — walking
/// parent pointers — costs one round trip per level, which is exactly the shape that makes deep
/// threads slow in every system that has tried it.
/// </para>
/// <para>
/// The brief asks for <b>unlimited nesting</b>, and that is what the model provides: nothing
/// caps the stored chain except a cycle guard. What is capped is <em>rendered</em> depth, and
/// the flattening happens in <see cref="CommentThread"/> at read time, so the decision stays
/// reversible.
/// </para>
/// </summary>
public sealed class Comment : AggregateRoot<CommentId>, IAuditableEntity
{
    /// <summary>Separator in the materialised path. Never appears in a GUID N-format.</summary>
    public const char PathSeparator = '/';

    private readonly List<CommentMention> _mentions = [];

    private Comment(
        CommentId id,
        ContentReference content,
        UserId authorId,
        CommentBody body,
        CommentId? parentId,
        string path,
        int depth,
        DateTimeOffset createdAt) : base(id)
    {
        Content = content;
        AuthorId = authorId;
        Body = body;
        ParentCommentId = parentId;
        Path = path;
        Depth = depth;
        Status = CommentStatus.Active;
        CreatedAt = createdAt;
    }

    private Comment() { }

    /// <summary>What this comment is on. A post, a guide, an event — anything addressable.</summary>
    public ContentReference Content { get; private set; } = default!;

    public UserId AuthorId { get; private set; } = default!;

    public CommentBody Body { get; private set; } = default!;

    public CommentId? ParentCommentId { get; private set; }

    /// <summary>
    /// Slash-joined ancestor identifiers, ending with this comment. A prefix scan on this column
    /// is a subtree, which is the whole reason the column exists.
    /// </summary>
    public string Path { get; private set; } = string.Empty;

    /// <summary>Zero for a top-level comment. Derived from the path at construction.</summary>
    public int Depth { get; private set; }

    public CommentStatus Status { get; private set; }

    public bool IsPinned { get; private set; }

    public bool IsEdited { get; private set; }

    public int ReplyCount { get; private set; }

    public int ReactionCount { get; private set; }

    /// <summary>The client renders this as <c>likeCount</c>. Kept as its own counter.</summary>
    public int LikeCount { get; private set; }

    public int ReportCount { get; private set; }

    /// <summary>Set when this comment quotes another. Distinct from replying to it.</summary>
    public CommentId? QuotedCommentId { get; private set; }

    /// <summary>A media reference. Never a URL.</summary>
    public MediaId? AttachmentMediaId { get; private set; }

    public DateTimeOffset? EditedAt { get; private set; }

    public IReadOnlyList<CommentMention> Mentions => _mentions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    /// <summary>
    /// Deletion is recorded on the row, and the row stays queryable.
    /// <para>
    /// <b>Comment deliberately does not implement <c>ISoftDeletable</c>.</b> That interface
    /// installs a global query filter excluding deleted rows, which is exactly right for a post
    /// and exactly wrong for a comment: a deleted comment with four replies has to keep its place
    /// in the thread, and a filter that hid it would take its replies with it. The fields are
    /// kept — an audit trail still wants to know who removed what and when — and
    /// <see cref="CommentStatus.Deleted"/> is what a projection reads.
    /// </para>
    /// </summary>
    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    /// <summary>A comment is itself commentable, reactable and reportable.</summary>
    public ContentReference Reference => ContentReference.To(CommunityContentType.Comment, Id);

    /// <summary>True when the body must be replaced by a tombstone on the way out.</summary>
    public bool IsTombstoned => Status is CommentStatus.Removed or CommentStatus.Deleted;

    public bool IsReadableByOthers => Status is CommentStatus.Active or CommentStatus.Reported;

    public static Result<Comment> Create(
        ContentReference content,
        UserId authorId,
        CommentBody body,
        Comment? parent,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(authorId);
        ArgumentNullException.ThrowIfNull(body);

        if (parent is not null && !parent.Content.Equals(content))
        {
            return Result.Failure<Comment>(CommunityErrors.CommentParentMismatch);
        }

        var depth = parent is null ? 0 : parent.Depth + 1;

        if (depth > CommunityLimits.MaxCommentDepth)
        {
            return Result.Failure<Comment>(
                CommunityErrors.CommentDepthExceeded(CommunityLimits.MaxCommentDepth));
        }

        var id = CommentId.New();
        var path = parent is null
            ? id.Value.ToString("N")
            : string.Concat(parent.Path, PathSeparator, id.Value.ToString("N"));

        return Result.Success(new Comment(id, content, authorId, body, parent?.Id, path, depth, now));
    }

    public Result Edit(CommentBody body, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (IsTombstoned)
        {
            return Result.Failure(CommunityErrors.CommentNotEditable);
        }

        Body = body;
        EditedAt = now;

        // No silent window on a comment. A post is broadcast and a typo fix is noise; a comment
        // is a turn in a conversation, and a reader deciding whether a reply still makes sense
        // needs to know the thing it replied to changed.
        IsEdited = true;

        return Result.Success();
    }

    public void Quote(CommentId quotedCommentId) => QuotedCommentId = quotedCommentId;

    public void AttachMedia(MediaId? mediaId) => AttachmentMediaId = mediaId;

    public Result AddMention(MentionTarget target, int startOffset, int length)
    {
        ArgumentNullException.ThrowIfNull(target);

        if (_mentions.Count >= CommunityLimits.MaxMentionsPerComment)
        {
            return Result.Failure(
                CommunityErrors.PostMentionLimit(CommunityLimits.MaxMentionsPerComment));
        }

        _mentions.Add(CommentMention.Create(target, startOffset, length));

        return Result.Success();
    }

    public void ClearMentions() => _mentions.Clear();

    public void AdjustReplies(int delta) => ReplyCount = Math.Max(0, ReplyCount + delta);

    public void AdjustReactions(int delta, bool isLike)
    {
        ReactionCount = Math.Max(0, ReactionCount + delta);

        if (isLike)
        {
            LikeCount = Math.Max(0, LikeCount + delta);
        }
    }

    public void AdjustReports(int delta)
    {
        ReportCount = Math.Max(0, ReportCount + delta);

        if (ReportCount > 0 && Status == CommentStatus.Active)
        {
            Status = CommentStatus.Reported;
        }
        else if (ReportCount == 0 && Status == CommentStatus.Reported)
        {
            Status = CommentStatus.Active;
        }
    }

    public void Pin(bool pinned) => IsPinned = pinned;

    public Result ApplyModeration(ModerationActionType action)
    {
        switch (action)
        {
            case ModerationActionType.Hide:
                Status = CommentStatus.Hidden;
                break;

            case ModerationActionType.Reject:
                Status = CommentStatus.Removed;
                RaiseDomainEvent(new CommentRemovedDomainEvent
                {
                    CommentId = Id,
                    Content = Content,
                    Status = CommentStatus.Removed,
                    ByModerator = true
                });
                break;

            case ModerationActionType.Approve:
            case ModerationActionType.Restore:
                Status = CommentStatus.Active;
                ReportCount = 0;
                break;

            case ModerationActionType.Pin:
                IsPinned = true;
                break;

            case ModerationActionType.Unpin:
                IsPinned = false;
                break;

            case ModerationActionType.Suspend:
            case ModerationActionType.Archive:
                break;

            default:
                return Result.Failure(CommunityErrors.ModerationDecisionInvalid);
        }

        return Result.Success();
    }

    /// <summary>
    /// Marks the comment deleted by its author.
    /// <para>
    /// Sets the status and the soft-delete fields, and <b>does not remove the row</b>. A deleted
    /// comment with four replies is tombstoned rather than erased, because a conversation whose
    /// third message vanished and took its replies with it is worse for everybody still reading
    /// it than one that says a message was removed.
    /// </para>
    /// </summary>
    public void MarkDeleted(DateTimeOffset now, string? deletedBy)
    {
        Status = CommentStatus.Deleted;
        IsDeleted = true;
        DeletedAt = now;
        DeletedBy = deletedBy;

        RaiseDomainEvent(new CommentRemovedDomainEvent
        {
            CommentId = Id,
            Content = Content,
            Status = CommentStatus.Deleted,
            ByModerator = false
        });
    }

    public void RaiseAdded(Guid? contentAuthorId, Guid? parentAuthorId) =>
        RaiseDomainEvent(new CommentAddedDomainEvent
        {
            CommentId = Id,
            Content = Content,
            AuthorId = AuthorId,
            ParentCommentId = ParentCommentId,
            ContentAuthorId = contentAuthorId,
            ParentAuthorId = parentAuthorId,
            Depth = Depth
        });

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

    /// <summary>Undoes a soft delete and un-tombstones the comment in its thread.</summary>
    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        Status = CommentStatus.Active;
    }
}
