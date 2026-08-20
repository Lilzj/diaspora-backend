using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Identifiers;

/// <summary>One thing a member did to a recommendation: viewed, clicked, accepted, dismissed or bookmarked.</summary>
public sealed record RecommendationFeedbackId(Guid Value) : StronglyTypedId(Value)
{
    public static RecommendationFeedbackId New() => new(Guid.NewGuid());

    public static RecommendationFeedbackId From(Guid value) => new(value);
}
