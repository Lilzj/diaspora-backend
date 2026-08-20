namespace Diaspora.Modules.Recommendations.Domain.Enums;

/// <summary>
/// What a policy does to a candidate it matches.
/// <para>
/// <b>Two effects, and the second one exists so the first can stay absolute.</b> "Never recommend
/// blocked content" must mean never — no weight, no threshold, no "unless the page would otherwise
/// be empty". Once exclusion is unconditional, the softer rules that would have eroded it need
/// somewhere else to live, and <see cref="Demote"/> is that place: a candidate the platform would
/// rather not lead with but has no business hiding.
/// </para>
/// </summary>
public enum PolicyEffect
{
    /// <summary>The candidate is removed from the pool. Unconditional, and never overridden by a score.</summary>
    Exclude = 0,

    /// <summary>The candidate's final score is multiplied by the policy's factor. Never below zero.</summary>
    Demote = 1
}
