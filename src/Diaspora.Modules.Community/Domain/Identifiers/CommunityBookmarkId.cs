using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one member's bookmark of one piece of content.</summary>
public sealed record CommunityBookmarkId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityBookmarkId New() => new(Guid.NewGuid());

    public static CommunityBookmarkId From(Guid value) => new(value);
}
