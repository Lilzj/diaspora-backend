using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.ValueObjects;

/// <summary>
/// What one strategy contributed to one recommendation's score.
/// <para>
/// <b>The arithmetic is stored, not just the answer.</b> A final score of 0.71 tells an operator
/// nothing; "location 0.9 × 0.4, popularity 0.3 × 0.2, trust 1.0 × 0.15" tells them the housing
/// surface is over-weighting proximity and lets them fix it by changing a catalogue row. This is the
/// difference between a system that can be tuned and one that can only be replaced.
/// </para>
/// <para>
/// It is also the AI seam's honesty check. When a future adapter registers a machine-learned
/// strategy, it contributes a row here like everything else — so a page ranked partly by a model
/// still reports which part, and the explainability contract survives contact with a black box.
/// </para>
/// </summary>
public sealed class StrategyContribution : ValueObject
{
    private StrategyContribution(
        Guid contributionId,
        string strategyKey,
        RankingStrategyKind kind,
        decimal rawScore,
        decimal weight)
    {
        ContributionId = contributionId;
        StrategyKey = strategyKey;
        Kind = kind;
        RawScore = rawScore;
        Weight = weight;
        WeightedScore = decimal.Round(rawScore * weight, 6);
    }

    private StrategyContribution() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid ContributionId { get; private set; }

    public string StrategyKey { get; private set; } = string.Empty;

    public RankingStrategyKind Kind { get; private set; }

    /// <summary>What the strategy said, before weighting. Always between 0 and 1.</summary>
    public decimal RawScore { get; private set; }

    /// <summary>What the surface's catalogue says this strategy is worth.</summary>
    public decimal Weight { get; private set; }

    /// <summary>The product. Stored rather than computed so a weight change cannot rewrite history.</summary>
    public decimal WeightedScore { get; private set; }

    public static StrategyContribution Create(
        Guid contributionId,
        string strategyKey,
        RankingStrategyKind kind,
        decimal rawScore,
        decimal weight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(strategyKey);

        return new StrategyContribution(
            contributionId,
            strategyKey.Trim().ToLowerInvariant(),
            kind,
            Clamp(rawScore),
            Math.Max(0m, weight));
    }

    private static decimal Clamp(decimal value) => Math.Clamp(value, 0m, 1m);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ContributionId;
        yield return StrategyKey;
        yield return Kind;
        yield return RawScore;
        yield return Weight;
        yield return WeightedScore;
    }
}
