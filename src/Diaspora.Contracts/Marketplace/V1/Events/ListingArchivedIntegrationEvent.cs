using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A listing was retired from every public surface by its seller.
/// <para>
/// A de-indexing signal. Distinct from <c>ListingDeleted</c> because the two mean different things
/// to a consumer holding a reference: an archived listing may come back, and a deleted one will
/// not. Anything holding a saved reference should keep it in the first case and drop it in the
/// second.
/// </para>
/// </summary>
public sealed record ListingArchivedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public Guid? ArchivedBy { get; init; }

    /// <summary>Why the seller took it down, when they said. Shown to anyone mid-negotiation.</summary>
    public string? Reason { get; init; }

    public required DateTimeOffset ArchivedAt { get; init; }
}
