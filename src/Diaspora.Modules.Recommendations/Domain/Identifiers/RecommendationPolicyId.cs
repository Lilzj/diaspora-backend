using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Identifiers;

/// <summary>One rule about what may never be recommended. Reference data.</summary>
public sealed record RecommendationPolicyId(Guid Value) : StronglyTypedId(Value)
{
    public static RecommendationPolicyId New() => new(Guid.NewGuid());

    public static RecommendationPolicyId From(Guid value) => new(value);
}
