using Diaspora.Modules.Bookmarks.Contracts;
using Diaspora.Modules.Bookmarks.Domain;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Bookmarks.Application;

/// <summary>Errors the module returns, in the same shape as every other module's.</summary>
public static class BookmarkErrors
{
    public static readonly Error NotAuthenticated =
        Error.Unauthorized("Bookmarks.NotAuthenticated", "You must be signed in.");

    public static readonly Error NotFound =
        Error.NotFound("Bookmarks.NotFound", "That bookmark does not exist.");

    /// <summary>
    /// The target type is not one any module claims.
    /// <para>
    /// Bookmarks deliberately does not check that the target <em>exists</em> — see
    /// <c>BookmarkTargetCatalog</c> for why — but a type nothing owns can never resolve to
    /// anything, so accepting it only creates a row no client can render.
    /// </para>
    /// </summary>
    public static Error UnknownTargetType(string targetType, IReadOnlyList<string> known) =>
        Error.Validation(
            "Bookmarks.UnknownTargetType",
            $"'{targetType}' is not something that can be bookmarked. "
            + $"Expected one of: {string.Join(", ", known)}.");
}

/// <summary>Aggregate-to-DTO mapping. Hand-written; the module owns its own edges.</summary>
public static class BookmarkMapper
{
    public static BookmarkResponse ToResponse(Bookmark bookmark)
    {
        ArgumentNullException.ThrowIfNull(bookmark);

        return new BookmarkResponse
        {
            Id = bookmark.Id.Value,
            TargetType = bookmark.TargetType,
            TargetId = bookmark.TargetId,
            Note = bookmark.Note,
            CreatedAt = bookmark.CreatedAt
        };
    }
}
