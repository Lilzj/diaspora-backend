namespace Diaspora.Modules.Recommendations.Extensibility.Policies;

/// <summary>
/// What a policy decided about one candidate.
/// <para>
/// <b>Three outcomes, not two, and the third is why exclusion can stay absolute.</b> Allow and
/// exclude are obvious; <see cref="Demote"/> exists so the rules that would otherwise have eroded
/// "never means never" have somewhere to live. Without it, every soft preference would arrive as a
/// request to weaken the exclusion path.
/// </para>
/// <para>
/// <b><see cref="Reason"/> is required on an exclusion.</b> A candidate that vanishes from a page
/// with no recorded cause is the hardest defect in this module to diagnose — the page looks thin,
/// nothing errored, and the pool was fine. The engine counts exclusions by reason and the diagnostic
/// endpoint reports them.
/// </para>
/// </summary>
public sealed record PolicyDecision
{
    private PolicyDecision(bool isExcluded, decimal factor, string? reason)
    {
        IsExcluded = isExcluded;
        Factor = factor;
        Reason = reason;
    }

    /// <summary>The candidate is removed from the pool. Unconditional.</summary>
    public bool IsExcluded { get; }

    /// <summary>What the final score is multiplied by. One when nothing was demoted.</summary>
    public decimal Factor { get; }

    /// <summary>Why, for an exclusion or a demotion. Required on an exclusion — see the class summary.</summary>
    public string? Reason { get; }

    /// <summary>The candidate passes this rule untouched.</summary>
    public static PolicyDecision Allow() => new(isExcluded: false, factor: 1m, reason: null);

    public static PolicyDecision Exclude(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new PolicyDecision(isExcluded: true, factor: 0m, reason: reason);
    }

    /// <summary>
    /// The candidate stays but is scored down.
    /// <para>
    /// The factor is clamped above zero. A demotion to zero would remove a candidate from the page
    /// while reporting itself in the diagnostics as a nudge, which is exactly the ambiguity the
    /// three-outcome shape exists to prevent.
    /// </para>
    /// </summary>
    public static PolicyDecision Demote(decimal factor, string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        return new PolicyDecision(isExcluded: false, factor: Math.Clamp(factor, 0.01m, 1m), reason: reason);
    }
}
