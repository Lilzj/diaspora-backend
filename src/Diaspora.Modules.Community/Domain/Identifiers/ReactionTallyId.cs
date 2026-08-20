using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one row inside an owned collection. Never leaves the aggregate.</summary>
public sealed record ReactionTallyId(Guid Value) : StronglyTypedId(Value)
{
    public static ReactionTallyId New() => new(Guid.NewGuid());

    public static ReactionTallyId From(Guid value) => new(value);
}
