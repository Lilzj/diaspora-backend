using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies the association between a post and a hashtag.</summary>
public sealed record PostHashtagId(Guid Value) : StronglyTypedId(Value)
{
    public static PostHashtagId New() => new(Guid.NewGuid());

    public static PostHashtagId From(Guid value) => new(value);
}
