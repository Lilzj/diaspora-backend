using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// A paid placement stopped.
/// <para>
/// <b>The withdrawal half, and it matters more than the grant.</b> A featured rail that only ever
/// gains entries ends up showing nothing but promotions that finished last month — which is
/// Marketplace's own reasoning for publishing both directions since B10.
/// </para>
/// <para>
/// <see cref="Status"/> distinguishes lapsed from cancelled from refunded, which are three different
/// things to anything measuring what a placement was worth.
/// </para>
/// </summary>
public sealed record PlacementEndedIntegrationEvent : IntegrationEvent
{
    public required Guid PlacementId { get; init; }

    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required string Kind { get; init; }

    /// <summary><c>Expired</c>, <c>Cancelled</c> or <c>Refunded</c>.</summary>
    public required string Status { get; init; }

    public required Guid PurchasedByUserId { get; init; }

    public required DateTimeOffset EndedAt { get; init; }
}
