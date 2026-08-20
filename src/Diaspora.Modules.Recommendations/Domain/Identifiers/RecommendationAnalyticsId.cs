using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Identifiers;

/// <summary>One day's measured performance for one surface. A rollup, not an event log.</summary>
public sealed record RecommendationAnalyticsId(Guid Value) : StronglyTypedId(Value)
{
    public static RecommendationAnalyticsId New() => new(Guid.NewGuid());

    public static RecommendationAnalyticsId From(Guid value) => new(value);
}
