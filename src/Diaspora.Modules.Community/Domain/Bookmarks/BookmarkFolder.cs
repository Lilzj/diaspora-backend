using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Bookmarks;

/// <summary>
/// A member-created folder of saved things.
/// <para>
/// The client groups saved items <em>by content type</em> — providers, guides, updates, jobs —
/// and calls those groups collections. That grouping is derived, not stored: it is a projection
/// over the bookmark table and needs no rows of its own.
/// </para>
/// <para>
/// This is the other thing, and the brief asks for both: a folder somebody named. "Flat hunt",
/// "Visa paperwork", "Places to eat" — spanning content types, which is exactly what the
/// type-derived collections cannot do. Smart collections, when they arrive, are a rule on this
/// row rather than a new concept.
/// </para>
/// </summary>
public sealed class BookmarkFolder : AggregateRoot<BookmarkFolderId>, IAuditableEntity
{
    public const int MaxNameLength = 100;

    private BookmarkFolder(
        BookmarkFolderId id, UserId ownerId, string name, string? glyph, int position) : base(id)
    {
        OwnerId = ownerId;
        Name = name;
        Glyph = glyph;
        Position = position;
    }

    private BookmarkFolder() { }

    public UserId OwnerId { get; private set; } = default!;

    public string Name { get; private set; } = string.Empty;

    /// <summary>An emoji the member picked. The client renders one beside every collection.</summary>
    public string? Glyph { get; private set; }

    public int Position { get; private set; }

    public int ItemCount { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    public static BookmarkFolder Create(
        UserId ownerId, string name, string? glyph, int position)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        var trimmed = (name ?? string.Empty).Trim();

        return new BookmarkFolder(
            BookmarkFolderId.New(),
            ownerId,
            trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed,
            glyph?.Trim(),
            position);
    }

    public void Rename(string name, string? glyph)
    {
        var trimmed = (name ?? string.Empty).Trim();

        if (trimmed.Length > 0)
        {
            Name = trimmed.Length > MaxNameLength ? trimmed[..MaxNameLength] : trimmed;
        }

        Glyph = glyph?.Trim();
    }

    public void AdjustItems(int delta) => ItemCount = Math.Max(0, ItemCount + delta);

    public void MoveTo(int position) => Position = position;

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
