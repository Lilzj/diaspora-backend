using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Identifiers;

/// <summary>One generated page for one member on one surface, with its explanations.</summary>
public sealed record RecommendationSetId(Guid Value) : StronglyTypedId(Value)
{
    public static RecommendationSetId New() => new(Guid.NewGuid());

    public static RecommendationSetId From(Guid value) => new(value);
}
