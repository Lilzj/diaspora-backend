namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// One page of official notice search results.
/// <para>
/// <b>Cards, not hits.</b> The engine returns identifiers and this response returns rendered cards,
/// because the resolution happens against the write model — which is the only thing that knows
/// whether a notice was superseded this morning. An external index a day behind can hand back a
/// stale identifier and the reader still sees the current truth.
/// </para>
/// <para>
/// <see cref="Engine"/> is carried for the same reason the outcome carries it: the answer to "why did
/// search behave differently in production" is almost always "a different engine is registered
/// there".
/// </para>
/// </summary>
public sealed record OfficialNoticeSearchResponse
{
    public IReadOnlyList<OfficialNoticeCardResponse> Results { get; init; } = [];

    /// <summary>The opaque continuation token, or null at the end of the results.</summary>
    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    /// <summary>How many results exist in total, when the engine can say cheaply. Frequently null.</summary>
    public int? TotalCount { get; init; }

    /// <summary>The engine that answered. Diagnostic.</summary>
    public required string Engine { get; init; }

    /// <summary>The phrase, normalized as the engine received it. Echoed for the empty state.</summary>
    public string? Query { get; init; }
}
