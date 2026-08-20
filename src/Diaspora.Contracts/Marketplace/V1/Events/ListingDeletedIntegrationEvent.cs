using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Marketplace.V1.Events;

/// <summary>
/// A listing was deleted.
/// <para>
/// Deletion is soft in this module's own store — the row survives for audit and for undelete — but
/// to every consumer it is final: drop the reference, remove the index entry, close the card. A
/// consumer that kept it would keep sending buyers to something the seller has withdrawn.
/// </para>
/// </summary>
public sealed record ListingDeletedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid SellerId { get; init; }

    public Guid? DeletedBy { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}
