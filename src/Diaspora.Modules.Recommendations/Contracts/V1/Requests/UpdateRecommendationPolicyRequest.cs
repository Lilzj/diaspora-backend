namespace Diaspora.Modules.Recommendations.Contracts.V1.Requests;

/// <summary>
/// A change to one recommendation policy.
/// <para>
/// <b>Disabling a system policy is refused by the aggregate, not by a validator here.</b> A
/// validator can be bypassed by a second write path; a domain rule cannot. The administration
/// response marks which policies are immutable so the interface does not offer an action that will
/// fail.
/// </para>
/// </summary>
public sealed record UpdateRecommendationPolicyRequest
{
    public bool? IsEnabled { get; init; }

    public int? Order { get; init; }

    /// <summary>Ignored unless the policy demotes. Clamped above zero — a demotion is not an exclusion.</summary>
    public decimal? DemotionFactor { get; init; }

    public string? Description { get; init; }

    /// <summary>Empty means every surface. Null leaves the scope as it is.</summary>
    public IReadOnlyList<string>? Surfaces { get; init; }

    /// <summary>Empty means every target type. Null leaves the scope as it is.</summary>
    public IReadOnlyList<string>? TargetTypes { get; init; }

    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}
