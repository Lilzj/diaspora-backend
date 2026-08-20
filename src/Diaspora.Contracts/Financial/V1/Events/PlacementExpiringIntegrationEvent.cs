using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A paid placement is about to lapse.
/// <para>
/// <b>The single highest-value missing event on the frontend's Phase 6 register, and it is missing
/// for the same reason as the subscription warning.</b> A member who paid to feature a listing had no
/// way of learning it was ending except by opening the app while it was still running. The lead time
/// comes from policy; the announcement is made once per placement.
/// </para>
/// </summary>
public sealed record PlacementExpiringIntegrationEvent : IntegrationEvent
{
    public required Guid PlacementId { get; init; }

    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required string Kind { get; init; }

    public required Guid PurchasedByUserId { get; init; }

    public required string ProductCode { get; init; }

    /// <summary>True when the buyer has asked for it to renew itself.</summary>
    public required bool AutoRenewEnabled { get; init; }

    public required DateTimeOffset EndsAt { get; init; }

    public required int LeadDays { get; init; }

    public required DateTimeOffset AnnouncedAt { get; init; }
}
