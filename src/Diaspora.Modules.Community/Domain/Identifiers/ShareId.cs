using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one share of one piece of content.</summary>
public sealed record ShareId(Guid Value) : StronglyTypedId(Value)
{
    public static ShareId New() => new(Guid.NewGuid());

    public static ShareId From(Guid value) => new(value);
}
