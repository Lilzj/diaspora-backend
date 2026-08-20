using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies a follow edge between a follower and a followee.</summary>
public sealed record FollowId(Guid Value) : StronglyTypedId(Value)
{
    public static FollowId New() => new(Guid.NewGuid());

    public static FollowId From(Guid value) => new(value);
}
