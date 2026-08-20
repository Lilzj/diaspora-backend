using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Recommendations.V1.Events;

/// <summary>
/// A member's personalization profile changed.
/// <para>
/// <b>Carries no interests and no affinities, and that omission is the privacy decision of the
/// module.</b> A profile is what the platform believes somebody wants, assembled from what they
/// looked at rather than from anything they said. Publishing its contents would put the most
/// inferential record the platform holds onto a bus every module reads, for the benefit of nobody
/// who needs it.
/// </para>
/// <para>
/// What travels is shape — how complete, whether cold, how many signals — which is enough for a
/// consumer to decide whether to ask this module a question and never enough to answer one itself.
/// </para>
/// <para>
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record PersonalizationProfileUpdatedIntegrationEvent : IntegrationEvent
{
    public required Guid ProfileId { get; init; }

    public required Guid UserId { get; init; }

    /// <summary>0–100. How much of the profile the platform has been able to fill in.</summary>
    public required int Completeness { get; init; }

    /// <summary>True while the platform still has too little to personalise from.</summary>
    public required bool IsColdStart { get; init; }

    public required int SignalCount { get; init; }

    /// <summary>True when the whole profile was recomputed rather than incrementally folded.</summary>
    public required bool WasRebuilt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }
}
