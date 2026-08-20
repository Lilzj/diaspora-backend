using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Housing.Extensibility.Ranking;

/// <summary>What a ranking strategy is allowed to know about the request.</summary>
public sealed record HousingRankingContext
{
    /// <summary>Who is asking. Null for a signed-out visitor.</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>When the request is being served. Passed in rather than read from the clock so a
    /// strategy is a pure function and therefore testable without freezing time.</summary>
    public required DateTimeOffset Now { get; init; }

    /// <summary>The viewer's point, when they supplied one. Null makes proximity ranking a no-op.</summary>
    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    /// <summary>The neighbourhood the search was narrowed to, when it was narrowed to one.</summary>
    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>Whether the caller asked for available properties only.</summary>
    public bool AvailableOnly { get; init; }
}
