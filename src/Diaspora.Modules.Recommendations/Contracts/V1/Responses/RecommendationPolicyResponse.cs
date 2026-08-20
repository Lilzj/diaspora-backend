namespace Diaspora.Modules.Recommendations.Contracts.V1.Responses;

/// <summary>
/// One configured recommendation policy.
/// <para>
/// <see cref="IsSystem"/> tells the administration screen which rows to render without a disable
/// control. The aggregate refuses the change regardless — the flag is so the interface does not
/// offer an action that will fail.
/// </para>
/// </summary>
public sealed record RecommendationPolicyResponse
{
    public required Guid PolicyId { get; init; }

    public required string Key { get; init; }

    public required string Kind { get; init; }

    /// <summary><c>Exclude</c> or <c>Demote</c>.</summary>
    public required string Effect { get; init; }

    public required string Description { get; init; }

    public required bool IsEnabled { get; init; }

    /// <summary>Cannot be disabled. See the class summary.</summary>
    public required bool IsSystem { get; init; }

    public required int Order { get; init; }

    /// <summary>Ignored unless the effect is a demotion.</summary>
    public required decimal DemotionFactor { get; init; }

    /// <summary>Empty means every surface.</summary>
    public required IReadOnlyList<string> Surfaces { get; init; }

    /// <summary>Empty means every target type.</summary>
    public required IReadOnlyList<string> TargetTypes { get; init; }

    public required IReadOnlyDictionary<string, string> Parameters { get; init; }

    /// <summary>Whether anything implements the key. A configured rule with no code enforces nothing.</summary>
    public required bool IsImplemented { get; init; }
}
