using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Recommendations.V1.Events;

/// <summary>
/// A page of recommendations was produced for a member.
/// <para>
/// <b>Carries what was generated, never what was recommended.</b> The item count, the surface and
/// the strategies are shape; the identifiers of the things a particular person was shown are the
/// most inferential record the platform holds about them, and putting that on a bus every module
/// reads would make the recommendation platform the platform's largest unnecessary disclosure
/// surface. B13 made the same call about notification text and for the same reason.
/// </para>
/// <para>
/// <see cref="IsColdStart"/> is the field most consumers will care about: it distinguishes "the
/// platform personalised a page" from "the platform had nothing to go on and used defaults", and the
/// two mean entirely different things to anything measuring quality.
/// </para>
/// <para>
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record RecommendationGeneratedIntegrationEvent : IntegrationEvent
{
    public required Guid SetId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>The surface the page was built for. <c>HomeFeed</c>, <c>Jobs</c>, and so on.</summary>
    public required string Surface { get; init; }

    public required int ItemCount { get; init; }

    /// <summary>The strategy keys that contributed a non-zero score to at least one item.</summary>
    public required IReadOnlyList<string> StrategyKeys { get; init; }

    /// <summary>True when the member had no usable profile and the cold-start path ran.</summary>
    public required bool IsColdStart { get; init; }

    /// <summary>How many candidates a policy removed before ranking.</summary>
    public required int PolicyExclusionCount { get; init; }

    public required DateTimeOffset GeneratedAt { get; init; }
}
