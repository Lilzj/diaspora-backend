using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Identifiers;

/// <summary>One member's derived profile. One row per member, rebuildable from their signals.</summary>
public sealed record PersonalizationProfileId(Guid Value) : StronglyTypedId(Value)
{
    public static PersonalizationProfileId New() => new(Guid.NewGuid());

    public static PersonalizationProfileId From(Guid value) => new(value);
}
