using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies a member-created folder of bookmarks.</summary>
public sealed record BookmarkFolderId(Guid Value) : StronglyTypedId(Value)
{
    public static BookmarkFolderId New() => new(Guid.NewGuid());

    public static BookmarkFolderId From(Guid value) => new(value);
}
