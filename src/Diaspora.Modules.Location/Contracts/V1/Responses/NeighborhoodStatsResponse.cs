namespace Diaspora.Modules.Location.Contracts.V1.Responses;

/// <summary>
/// The liveability indexes a neighbourhood hub renders.
/// <para>
/// All three are nullable, and today all three are null. The frontend's mock invents them
/// per request from the city name; inventing them on the server would be the same fiction
/// with more authority behind it. The shape is present so that no contract change is needed
/// when a data source arrives — the fields simply start being populated. Known gap #1.
/// </para>
/// </summary>
public sealed record NeighborhoodStatsResponse
{
    /// <summary>0–100.</summary>
    public int? SafetyScore { get; init; }

    /// <summary><c>Low</c>, <c>Medium</c> or <c>High</c>.</summary>
    public string? CostOfLiving { get; init; }

    /// <summary>0–100.</summary>
    public int? Walkability { get; init; }

    /// <summary>
    /// False while no index has been sourced. Explicit so a client can render "not yet rated"
    /// rather than three zeroes, which read as a very bad neighbourhood.
    /// </summary>
    public required bool IsAvailable { get; init; }
}
