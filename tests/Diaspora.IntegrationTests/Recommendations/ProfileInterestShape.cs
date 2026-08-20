namespace Diaspora.IntegrationTests.Recommendations;

/// <summary>One derived or stated interest on the wire.</summary>
public sealed record ProfileInterestShape
{
    public string? Kind { get; init; }

    public string? Key { get; init; }

    public decimal Affinity { get; init; }

    public int ObservationCount { get; init; }

    /// <summary>What lets a client separate "you told us" from "we noticed".</summary>
    public bool IsExplicit { get; init; }
}
