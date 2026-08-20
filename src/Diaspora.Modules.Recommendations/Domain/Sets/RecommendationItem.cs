using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Sets;

/// <summary>
/// One recommendation, with the arithmetic that produced it.
/// <para>
/// <b>There is no constructor that omits the explanation, and that is how "every recommendation
/// includes an explanation" stops being a promise and becomes a property of the type.</b> A
/// reviewer cannot forget it, a refactor cannot drop it, and a future ranking adapter cannot skip
/// it — the compiler will not produce an unexplained item.
/// </para>
/// <para>
/// <b>The score is stored, not recomputed.</b> A member who asks "why am I seeing this" ten minutes
/// after the page rendered must get the reason the page was actually built with. Recomputing it
/// against a profile that has since folded in four more signals would produce a different, equally
/// truthful, and completely useless answer.
/// </para>
/// <para>
/// Owned by <see cref="RecommendationSet"/>, and owning its own contributions in turn. Two levels of
/// owned collection is unusual in this platform; it is right here because a contribution has no
/// meaning outside the item and an item has none outside the set, and the alternative — three
/// aggregates with foreign keys — would let a contribution outlive the recommendation it explains.
/// </para>
/// </summary>
public sealed class RecommendationItem : ValueObject
{
    private readonly List<StrategyContribution> _contributions = [];

    private RecommendationItem(
        Guid itemId,
        TargetReference target,
        int position,
        decimal score,
        string primaryStrategyKey,
        RecommendationExplanation explanation)
    {
        ItemId = itemId;
        Target = target;
        Position = position;
        Score = score;
        PrimaryStrategyKey = primaryStrategyKey;
        Explanation = explanation;
    }

    private RecommendationItem() { }

    /// <summary>Caller-supplied key. The configuration declares <c>ValueGeneratedNever</c>.</summary>
    public Guid ItemId { get; private set; }

    public TargetReference Target { get; private set; } = default!;

    /// <summary>Zero-based rank on the page as generated. Frozen — the client may reorder, the record may not.</summary>
    public int Position { get; private set; }

    /// <summary>The weighted sum, after any demotion. Between 0 and 1.</summary>
    public decimal Score { get; private set; }

    /// <summary>Whichever strategy contributed most. What analytics attribute the item to.</summary>
    public string PrimaryStrategyKey { get; private set; } = string.Empty;

    /// <summary>Why. Required by construction — see the class summary.</summary>
    public RecommendationExplanation Explanation { get; private set; } = default!;

    /// <summary>The per-strategy arithmetic. Empty only for a cold-start item with one contributor.</summary>
    public IReadOnlyList<StrategyContribution> Contributions => _contributions.AsReadOnly();

    public static RecommendationItem Create(
        Guid itemId,
        TargetReference target,
        int position,
        decimal score,
        string primaryStrategyKey,
        RecommendationExplanation explanation,
        IEnumerable<StrategyContribution>? contributions = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(explanation);
        ArgumentException.ThrowIfNullOrWhiteSpace(primaryStrategyKey);

        var item = new RecommendationItem(
            itemId,
            target,
            Math.Max(0, position),
            decimal.Round(Math.Clamp(score, 0m, 1m), 6),
            primaryStrategyKey.Trim().ToLowerInvariant(),
            explanation);

        foreach (var contribution in contributions ?? [])
        {
            item._contributions.Add(contribution);
        }

        return item;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ItemId;
        yield return Target;
        yield return Position;
        yield return Score;
        yield return PrimaryStrategyKey;
    }
}
