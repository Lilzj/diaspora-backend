namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// A personalization hint for a search engine this module does not own.
/// <para>
/// A multiplier and a reason, never a result. Search owns matching; this decides which of the
/// matches a particular member is likelier to want, and returning ranked results would make this a
/// second search implementation. Shaped for a function-score query, which is what makes the
/// anticipated OpenSearch integration a mapping rather than a redesign.
/// </para>
/// </summary>
public sealed record SearchBoostResponse
{
    /// <summary><c>country</c>, <c>city</c>, <c>category</c>, <c>tag</c>, <c>language</c>.</summary>
    public required string Field { get; init; }

    public required string Value { get; init; }

    /// <summary>A multiplier. One is neutral; above one favours.</summary>
    public required decimal Boost { get; init; }

    public required string ReasonCode { get; init; }
}
