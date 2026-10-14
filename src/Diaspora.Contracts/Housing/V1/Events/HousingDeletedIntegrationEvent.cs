using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A home listing was deleted by its landlord or by staff.
/// <para>
/// Added 14 Oct 2026. Deletion was a domain event only, so nothing outside Housing heard of it,
/// and a deleted home stayed in members' recommendation feeds.
/// </para>
/// </summary>
public sealed record HousingDeletedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    public required DateTimeOffset DeletedAt { get; init; }
}
