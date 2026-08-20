using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Bookmarks;

/// <summary>
/// One member saving one thing.
/// <para>
/// Named <c>CommunityBookmark</c> rather than <c>Bookmark</c> because the B2.6 reference module
/// already owns that name and that route. The two are genuinely different: that one is a flat
/// list proving the contributor model works; this one spans every content type on the platform,
/// carries folders, and is the thing the client's saved screens actually render.
/// </para>
/// <para>
/// Unique per member and content — saving twice is a no-op, not a second row — because the only
/// question anybody asks of this table is whether a member saved a thing.
/// </para>
/// </summary>
public sealed class CommunityBookmark : AggregateRoot<CommunityBookmarkId>, IAuditableEntity
{
    public const int MaxNoteLength = 500;

    private CommunityBookmark(
        CommunityBookmarkId id,
        UserId userId,
        ContentReference content,
        string contentKey,
        BookmarkFolderId? folderId,
        DateTimeOffset savedAt) : base(id)
    {
        UserId = userId;
        Content = content;
        ContentKey = contentKey;
        FolderId = folderId;
        SavedAt = savedAt;
    }

    private CommunityBookmark() { }

    public UserId UserId { get; private set; } = default!;

    public ContentReference Content { get; private set; } = default!;

    /// <summary>
    /// Member and content in one column, so one-save-per-member is a real unique index. An index
    /// cannot span an owned type's columns and the aggregate's own — the constraint B5 recorded.
    /// </summary>
    public string ContentKey { get; private set; } = string.Empty;

    public BookmarkFolderId? FolderId { get; private set; }

    /// <summary>A private note. Never projected to anybody but its owner.</summary>
    public string? Note { get; private set; }

    public DateTimeOffset SavedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static CommunityBookmark Create(
        UserId userId,
        ContentReference content,
        DateTimeOffset now,
        BookmarkFolderId? folderId = null,
        string? note = null,
        Guid? contentAuthorId = null)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(content);

        var bookmark = new CommunityBookmark(
            CommunityBookmarkId.New(),
            userId,
            content,
            BuildKey(userId, content),
            folderId,
            now);

        bookmark.SetNote(note);

        bookmark.RaiseDomainEvent(new BookmarkAddedDomainEvent
        {
            BookmarkId = bookmark.Id,
            Content = content,
            UserId = userId,
            FolderId = folderId?.Value,
            ContentAuthorId = contentAuthorId
        });

        return bookmark;
    }

    public static string BuildKey(UserId userId, ContentReference content)
    {
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(content);

        return $"{userId.Value:N}:{content.Key}";
    }

    public void MoveTo(BookmarkFolderId? folderId) => FolderId = folderId;

    public void SetNote(string? note)
    {
        var trimmed = note?.Trim();

        Note = string.IsNullOrEmpty(trimmed)
            ? null
            : trimmed.Length > MaxNoteLength ? trimmed[..MaxNoteLength] : trimmed;
    }

    public void RaiseRemoved() =>
        RaiseDomainEvent(new BookmarkRemovedDomainEvent { Content = Content, UserId = UserId });

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
}
