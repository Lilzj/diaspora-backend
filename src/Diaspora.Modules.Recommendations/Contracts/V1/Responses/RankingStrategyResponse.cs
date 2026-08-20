namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// One configured ranking strategy.
/// <para>
/// <b><see cref="IsImplemented"/> is the field an operator most needs and would not think to ask
/// for.</b> A row naming a key nothing implements contributes silent zeroes to every score on its
/// surface, and the symptom — a ranking that feels slightly wrong — points at nothing. Returning the
/// registry's answer alongside the catalogue's row makes the mismatch visible on the screen where
/// somebody would fix it.
/// </para>
/// </summary>
public sealed record RankingStrategyResponse
{
    public required Guid StrategyId { get; init; }

    public required string Key { get; init; }

    public required string Kind { get; init; }

    public required string Surface { get; init; }

    public string? TargetType { get; init; }

    public required string Description { get; init; }

    /// <summary>0–1. What this strategy's raw score is multiplied by before summing.</summary>
    public required decimal Weight { get; init; }

    public required int Order { get; init; }

    public required bool IsEnabled { get; init; }

    /// <summary>Whether this strategy runs for a member the platform knows nothing about.</summary>
    public required bool RunsOnColdStart { get; init; }

    public required decimal MinimumScore { get; init; }

    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    /// <summary>Whether anything actually implements the key. See the class summary.</summary>
    public required bool IsImplemented { get; init; }
}
