using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one saved revision of a post's body.</summary>
public sealed record PostRevisionId(Guid Value) : StronglyTypedId(Value)
{
    public static PostRevisionId New() => new(Guid.NewGuid());

    public static PostRevisionId From(Guid value) => new(value);
}
