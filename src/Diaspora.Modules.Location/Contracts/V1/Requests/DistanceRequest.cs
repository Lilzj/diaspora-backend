namespace Diaspora.Modules.Location.Contracts.V1.Requests;

/// <summary>Asks how far apart two points are.</summary>
public sealed record DistanceRequest
{
    public required double FromLatitude { get; init; }

    public required double FromLongitude { get; init; }

    public required double ToLatitude { get; init; }

    public required double ToLongitude { get; init; }
}
