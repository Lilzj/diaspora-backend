using Diaspora.Modules.Recommendations.Domain.Enums;
using Diaspora.Modules.Recommendations.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Recommendations.Domain.Events;

/// <summary>
/// A set of recommendations was produced for a member.
/// <para>
/// <b>Raised on generation rather than on delivery, and the two are genuinely different
/// moments.</b> A set can be generated and never rendered — a client that abandons the request, a
/// prefetch that is thrown away — which is precisely why the analytics rollup counts generations and
/// views separately. Treating them as one number is how a surface with a 4% click rate is reported
/// as having a 40% one.
/// </para>
/// </summary>
public sealed record RecommendationGeneratedDomainEvent : DomainEvent
{
    public required RecommendationSetId SetId { get; init; }

    public required UserId UserId { get; init; }

    public required RecommendationSurface Surface { get; init; }

    public required int ItemCount { get; init; }

    /// <summary>The strategy keys that contributed at least one non-zero score.</summary>
    public required IReadOnlyList<string> StrategyKeys { get; init; }

    /// <summary>True when the member had no usable profile and the cold-start path ran.</summary>
    public required bool IsColdStart { get; init; }

    /// <summary>How many candidates a policy removed before ranking. The number nobody else can see.</summary>
    public required int PolicyExclusionCount { get; init; }

    /// <summary>
    /// The subjects in this set that ranked where they did <em>because</em> they were promoted.
    /// <para>
    /// Empty for almost every set. Present so B15 can count paid impressions without a
    /// synchronous call back into this module on the read path — see
    /// <c>PromotedTargetPayload</c>.
    /// </para>
    /// </summary>
    public required IReadOnlyList<PromotedTarget> PromotedTargets { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}

/// <summary>One subject served because it was promoted. See the event's own summary.</summary>
public sealed record PromotedTarget
{
    public required RecommendationTargetType TargetType { get; init; }

    public required Guid TargetId { get; init; }

    public required int Position { get; init; }
}
