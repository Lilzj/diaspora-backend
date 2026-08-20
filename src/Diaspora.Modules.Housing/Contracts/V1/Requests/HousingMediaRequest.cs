namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>A Media asset to attach, by reference.</summary>
public sealed record HousingMediaRequest
{
    /// <summary>B5's asset identifier. Never a URL and never bytes.</summary>
    public required Guid MediaId { get; init; }

    /// <summary>Cover, Gallery, FloorPlan, Video, VirtualTour, Document, LeaseDocument.</summary>
    public required string Role { get; init; }

    /// <summary>The landlord caption, such as "kitchen, looking north".</summary>
    public string? Caption { get; init; }
}
