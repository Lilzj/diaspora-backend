using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Identifiers;

/// <summary>One weighted ranking strategy, configured per surface. Reference data.</summary>
public sealed record RankingStrategyId(Guid Value) : StronglyTypedId(Value)
{
    public static RankingStrategyId New() => new(Guid.NewGuid());

    public static RankingStrategyId From(Guid value) => new(value);
}
