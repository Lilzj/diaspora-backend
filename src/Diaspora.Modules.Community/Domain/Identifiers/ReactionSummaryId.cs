using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies the per-content reaction summary row.</summary>
public sealed record ReactionSummaryId(Guid Value) : StronglyTypedId(Value)
{
    public static ReactionSummaryId New() => new(Guid.NewGuid());

    public static ReactionSummaryId From(Guid value) => new(value);
}
