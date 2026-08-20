using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Identifiers;

/// <summary>One recommendable thing, projected from a published contract. A shadow, never a source of truth.</summary>
public sealed record RecommendationCandidateId(Guid Value) : StronglyTypedId(Value)
{
    public static RecommendationCandidateId New() => new(Guid.NewGuid());

    public static RecommendationCandidateId From(Guid value) => new(value);
}
