namespace Diaspora.Modules.Housing.Contracts.V1.Requests;

/// <summary>A language heard in a neighbourhood, and how widely.</summary>
public sealed record NeighborhoodLanguageRequest
{
    /// <summary>
    /// The name as members would say it. Free text rather than an ISO code: the answer is often
    /// Krio or Patois, which registries handle badly and members use daily.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>A five-band prominence: Unknown, VeryLow, Low, Moderate, High, VeryHigh.</summary>
    public string? Prominence { get; init; }
}
