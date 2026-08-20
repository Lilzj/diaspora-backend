using Diaspora.Modules.Recommendations.Domain.Enums;

namespace Diaspora.Modules.Recommendations.Extensibility.Ranking;

/// <summary>
/// What one strategy said about one candidate.
/// <para>
/// <b>A score and a reason, never a score alone, and that pairing is the explainability contract
/// enforced at the seam.</b> A strategy that could return a bare number would let an adapter
/// contribute to a page without contributing to its explanation — which is precisely the failure
/// mode the brief rules out, and precisely what a machine-learned ranker would do if the seam
/// allowed it. The type does not allow it.
/// </para>
/// <para>
/// <see cref="Value"/> is always between 0 and 1. Weighting happens outside the strategy, so a
/// strategy cannot quietly out-weigh its own catalogue row by returning a large number.
/// </para>
/// </summary>
public sealed record RankingScore
{
    /// <summary>Between 0 and 1. Clamped by the engine regardless of what a strategy returns.</summary>
    public required decimal Value { get; init; }

    /// <summary>Why this strategy scored it this way. Required — see the class summary.</summary>
    public required RecommendationReasonCode ReasonCode { get; init; }

    /// <summary>
    /// A detail the reason builder may fold into the wording — a city name, a category, an author.
    /// <para>
    /// Carried as a string rather than a structured value because it is one substitution in one
    /// translated sentence, and a structured argument would push the wording back into the module
    /// that is deliberately not deciding it.
    /// </para>
    /// </summary>
    public string? ReasonDetail { get; init; }

    /// <summary>Which signal kinds this strategy drew on. Feeds the explanation and the confidence.</summary>
    public IReadOnlyList<SignalKind> SignalKinds { get; init; } = [];

    /// <summary>A strategy with nothing to say. Returned rather than omitted, so the pass stays total.</summary>
    public static RankingScore None() =>
        new() { Value = 0m, ReasonCode = RecommendationReasonCode.Unspecified };

    public bool IsMeaningful => Value > 0m;
}
