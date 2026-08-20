using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one row inside an owned collection. Never leaves the aggregate.</summary>
public sealed record VisibilityClauseId(Guid Value) : StronglyTypedId(Value)
{
    public static VisibilityClauseId New() => new(Guid.NewGuid());

    public static VisibilityClauseId From(Guid value) => new(value);
}
