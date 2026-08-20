namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// A page of recommendations, with the diagnostics that explain its shape.
/// <para>
/// <b><see cref="CandidateCount"/> and <see cref="ExcludedCount"/> are returned to the client, not
/// just logged, and the reason is that a thin page is otherwise unexplainable.</b> Four items because
/// the country has four jobs and four items because a misconfigured policy removed ninety-six look
/// identical from outside. A client rendering an empty state can say "nothing here yet" in the first
/// case and an operator can diagnose the second, and neither is possible without these two numbers.
/// </para>
/// <para>
/// <see cref="SetId"/> is what a client sends back with a view, a click, an acceptance or a
/// dismissal. Without it the feedback cannot be attributed to a strategy, and strategy performance —
/// the whole quality loop of the module — becomes unmeasurable.
/// </para>
/// </summary>
public sealed record RecommendationPageResponse
{
    public required IReadOnlyList<RecommendationResponse> Items { get; init; }

    /// <summary>The generated set. Sent back with every interaction — see the class summary.</summary>
    public Guid? SetId { get; init; }

    public required string Surface { get; init; }

    /// <summary>True when the platform had nothing to personalise from and used defaults.</summary>
    public required bool IsColdStart { get; init; }

    /// <summary>True when a stored page was reused rather than ranked again.</summary>
    public required bool IsCached { get; init; }

    /// <summary>How many candidates were considered before policies ran.</summary>
    public required int CandidateCount { get; init; }

    /// <summary>How many a policy removed. See the class summary.</summary>
    public required int ExcludedCount { get; init; }

    /// <summary>The strategies that contributed. What a debugging screen lists.</summary>
    public required IReadOnlyList<string> Strategies { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}
