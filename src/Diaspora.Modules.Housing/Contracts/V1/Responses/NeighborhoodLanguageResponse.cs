namespace Diaspora.Modules.Housing.Contracts.V1.Responses;

/// <summary>A language heard in a neighbourhood, and how widely.</summary>
public sealed record NeighborhoodLanguageResponse
{
    public required string Name { get; init; }

    /// <summary>A five-band prominence rather than a percentage. Nobody measured a percentage.</summary>
    public required string Prominence { get; init; }
}
