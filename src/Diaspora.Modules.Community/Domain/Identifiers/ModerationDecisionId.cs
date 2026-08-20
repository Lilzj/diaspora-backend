using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies one moderator decision.</summary>
public sealed record ModerationDecisionId(Guid Value) : StronglyTypedId(Value)
{
    public static ModerationDecisionId New() => new(Guid.NewGuid());

    public static ModerationDecisionId From(Guid value) => new(value);
}
