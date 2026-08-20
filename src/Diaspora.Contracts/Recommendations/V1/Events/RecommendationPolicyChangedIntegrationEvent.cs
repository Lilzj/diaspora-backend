using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Recommendations.V1.Events;

/// <summary>
/// A recommendation policy was created, changed, enabled or disabled.
/// <para>
/// <b>Published because a policy change is invisible from the outside and enormous in effect.</b>
/// Disabling the country-visibility policy does not fail, does not log an error and does not change
/// any response shape — it silently starts showing every member content from every country. This
/// event is what puts that on an audit trail somebody reads, and it is the reason the contract exists
/// even though no module in the platform consumes it today.
/// </para>
/// <para>
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record RecommendationPolicyChangedIntegrationEvent : IntegrationEvent
{
    public required Guid PolicyId { get; init; }

    public required string PolicyKey { get; init; }

    /// <summary><c>BlockedContent</c>, <c>MutedCategory</c>, and so on.</summary>
    public required string Kind { get; init; }

    /// <summary><c>Exclude</c> or <c>Demote</c>.</summary>
    public required string Effect { get; init; }

    public required bool IsEnabled { get; init; }

    /// <summary><c>created</c>, <c>updated</c>, <c>enabled</c> or <c>disabled</c>.</summary>
    public required string ChangeType { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
