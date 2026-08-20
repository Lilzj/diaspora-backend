using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Identifiers;

/// <summary>One member's stated wishes. One row per member, never derived and never rebuilt.</summary>
public sealed record PersonalizationPreferenceId(Guid Value) : StronglyTypedId(Value)
{
    public static PersonalizationPreferenceId New() => new(Guid.NewGuid());

    public static PersonalizationPreferenceId From(Guid value) => new(value);
}
