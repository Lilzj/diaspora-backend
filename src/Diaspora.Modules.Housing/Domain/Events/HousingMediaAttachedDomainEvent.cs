using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Housing.Domain.Events;

/// <summary>
/// Raised when a photograph, floor plan or document is attached.
/// <para>
/// Published outward, unlike most content changes, because Media needs to hear it: an asset
/// nothing ever claimed is swept, and a listing whose gallery was uploaded and never claimed loses
/// its photographs a day later - silently, because every upload and every attach succeeded.
/// </para>
/// </summary>
public sealed record HousingMediaAttachedDomainEvent : DomainEvent
{
    public required HousingListingId ListingId { get; init; }

    public required LandlordProfileId LandlordId { get; init; }

    public required Guid MediaId { get; init; }

    public required HousingMediaRole Role { get; init; }
}
