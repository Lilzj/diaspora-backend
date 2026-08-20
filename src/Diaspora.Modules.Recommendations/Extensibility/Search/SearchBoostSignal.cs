using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Extensibility.Search;

/// <summary>
/// A personalization hint for a search engine this module does not own.
/// <para>
/// <b>A multiplier and a reason, not a result.</b> Search decides what matches a query; this decides
/// which of the matches this particular member is likelier to want. Returning ranked results here
/// would make the recommendation platform a second search implementation, and the two would
/// immediately disagree about what a query means.
/// </para>
/// <para>
/// Shaped for the OpenSearch integration the brief anticipates: a boost per field value is exactly
/// what a function-score query consumes, so wiring this to an index is a mapping rather than a
/// redesign.
/// </para>
/// </summary>
public sealed record SearchBoostSignal
{
    /// <summary>What the boost applies to: <c>country</c>, <c>category</c>, <c>tag</c>, <c>language</c>.</summary>
    public required string Field { get; init; }

    public required string Value { get; init; }

    /// <summary>A multiplier. One is neutral; above one favours, below one disfavours.</summary>
    public required decimal Boost { get; init; }

    /// <summary>Why, so the boost is as explainable as a recommendation is.</summary>
    public required RecommendationReasonCode ReasonCode { get; init; }
}
