using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Bookmarks.Domain;

/// <summary>
/// A bookmark's identifier.
/// <para>
/// Declared in the module, not in the SharedKernel's shared id list. A module owning its
/// own identifier type is part of the proof: it does not need a platform-wide file edited
/// to introduce an entity. The trade is that the module's persistence contributor must
/// register the id's value converter itself, which it does — see
/// <c>BookmarkEntityConfiguration</c>.
/// </para>
/// </summary>
public sealed record BookmarkId(Guid Value) : StronglyTypedId(Value)
{
    public static BookmarkId New() => new(Guid.NewGuid());

    public static BookmarkId From(Guid value) => new(value);
}
