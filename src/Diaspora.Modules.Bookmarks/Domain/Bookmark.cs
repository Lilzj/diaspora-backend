using Diaspora.Domain.Abstractions;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Bookmarks.Domain;

/// <summary>
/// A member's saved reference to something elsewhere on the platform — a listing, a guide,
/// a job.
/// <para>
/// A real aggregate, small on purpose. The point of this module is not the domain; it is
/// that a genuine aggregate with an invariant, a factory and audit fields drops into the
/// shared persistence, the CQRS pipeline and the authorisation catalogues without any of
/// them knowing it exists. If a toy this size needs a kernel change to land, so would
/// Marketplace.
/// </para>
/// </summary>
public sealed class Bookmark : AggregateRoot<BookmarkId>, IAuditableEntity
{
    public const int MaxTargetTypeLength = 64;
    public const int MaxNoteLength = 280;

    public UserId UserId { get; private set; } = default!;

    /// <summary>What kind of thing is saved — "marketplace-listing", "guide", "job".</summary>
    public string TargetType { get; private set; } = default!;

    /// <summary>The saved thing's identifier, as a string so any module's id shape fits.</summary>
    public string TargetId { get; private set; } = default!;

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private Bookmark() { }

    private Bookmark(
        BookmarkId id,
        UserId userId,
        string targetType,
        string targetId,
        string? note) : base(id)
    {
        UserId = userId;
        TargetType = targetType;
        TargetId = targetId;
        Note = note;
    }

    public static Bookmark Create(UserId userId, string targetType, string targetId, string? note)
    {
        ArgumentNullException.ThrowIfNull(userId);

        var type = Guard.Against.NullOrWhiteSpace(targetType, nameof(targetType)).Trim().ToLowerInvariant();
        Guard.Against.MaxLength(type, MaxTargetTypeLength, nameof(targetType));

        var target = Guard.Against.NullOrWhiteSpace(targetId, nameof(targetId)).Trim();

        return new Bookmark(BookmarkId.New(), userId, type, target, Normalize(note));
    }

    public void UpdateNote(string? note) => Note = Normalize(note);

    private static string? Normalize(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var trimmed = note.Trim();
        return trimmed.Length <= MaxNoteLength ? trimmed : trimmed[..MaxNoteLength];
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
