namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>Attaches one Media asset to a property, by reference.</summary>
public sealed record AttachHousingMediaRequest
{
    public required Guid MediaId { get; init; }

    /// <summary>Cover, Gallery, FloorPlan, Video, VirtualTour, Document, LeaseDocument.</summary>
    public required string Role { get; init; }

    public string? Caption { get; init; }
}
