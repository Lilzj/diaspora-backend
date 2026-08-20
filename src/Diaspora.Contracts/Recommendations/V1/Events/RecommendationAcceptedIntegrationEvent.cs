using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Recommendations.V1.Events;

/// <summary>
/// A member acted on a recommendation.
/// <para>
/// <b>The one event from this module that names a specific thing, and it does so because the member
/// has already acted on it publicly.</b> Somebody who applied for the job the platform recommended
/// has told the Jobs module about it directly; withholding the fact that a recommendation led there
/// would protect nothing and would make it impossible for a vertical to know its own attribution.
/// </para>
/// <para>
/// <see cref="StrategyKey"/> and <see cref="ReasonCode"/> are what make the event worth publishing:
/// they let a vertical answer "which of our items are found through recommendation, and why" without
/// this module knowing anything about that vertical.
/// </para>
/// <para>
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record RecommendationAcceptedIntegrationEvent : IntegrationEvent
{
    public required Guid FeedbackId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary><c>Job</c>, <c>HousingListing</c>, <c>Guide</c>, and so on.</summary>
    public required string TargetType { get; init; }

    public required Guid TargetId { get; init; }

    public required string Surface { get; init; }

    /// <summary>The strategy that put it on the page, when the generating set is still known.</summary>
    public string? StrategyKey { get; init; }

    /// <summary>The reason the member was shown, as a code. Never a rendered sentence.</summary>
    public string? ReasonCode { get; init; }

    /// <summary>What the platform scored it at. The number acceptance rate is measured against.</summary>
    public decimal? Score { get; init; }

    public required DateTimeOffset AcceptedAt { get; init; }
}
