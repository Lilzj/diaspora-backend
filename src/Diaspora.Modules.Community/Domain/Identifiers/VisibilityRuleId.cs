using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Community.Domain.Identifiers;

/// <summary>Identifies a stored audience rule.</summary>
public sealed record VisibilityRuleId(Guid Value) : StronglyTypedId(Value)
{
    public static VisibilityRuleId New() => new(Guid.NewGuid());

    public static VisibilityRuleId From(Guid value) => new(value);
}
