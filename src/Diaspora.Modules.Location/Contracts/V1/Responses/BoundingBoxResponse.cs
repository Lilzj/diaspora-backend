namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>A latitude/longitude rectangle, as a client would set a map viewport from.</summary>
public sealed record BoundingBoxResponse
{
    public required double North { get; init; }

    public required double South { get; init; }

    public required double East { get; init; }

    public required double West { get; init; }
}
