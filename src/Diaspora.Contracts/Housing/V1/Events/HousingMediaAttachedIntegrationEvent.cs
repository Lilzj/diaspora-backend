using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Housing.V1.Events;

/// <summary>
/// A photograph, floor plan or document was attached to a property.
/// <para>
/// Published mainly for Media own accounting: an asset nothing ever claimed is swept, and a listing
/// whose gallery was uploaded and never claimed loses its photographs a day later - silently,
/// because every upload and every attach succeeded.
/// </para>
/// </summary>
public sealed record HousingMediaAttachedIntegrationEvent : IntegrationEvent
{
    public required Guid ListingId { get; init; }

    public required Guid LandlordId { get; init; }

    /// <summary>The Media asset identifier. Never a URL and never bytes.</summary>
    public required Guid MediaId { get; init; }

    /// <summary>Cover, Gallery, FloorPlan, Video, VirtualTour, Document or LeaseDocument.</summary>
    public required string Role { get; init; }

    public required DateTimeOffset AttachedAt { get; init; }
}
