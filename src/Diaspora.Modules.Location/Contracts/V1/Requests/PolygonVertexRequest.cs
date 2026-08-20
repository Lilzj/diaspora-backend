namespace Diaspora.Modules.Location.Contracts.V1.Requests;

/// <summary>One corner of a search polygon.</summary>
public sealed record PolygonVertexRequest
{
    public required double Latitude { get; init; }

    public required double Longitude { get; init; }
}
