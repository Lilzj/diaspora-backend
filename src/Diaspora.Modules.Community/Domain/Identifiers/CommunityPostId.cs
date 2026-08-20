using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies a <see cref="Posts.CommunityPost"/>.</summary>
public sealed record CommunityPostId(Guid Value) : StronglyTypedId(Value)
{
    public static CommunityPostId New() => new(Guid.NewGuid());

    public static CommunityPostId From(Guid value) => new(value);
}
