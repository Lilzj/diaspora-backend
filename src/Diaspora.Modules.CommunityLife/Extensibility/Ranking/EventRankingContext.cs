namespace Diaspora.Modules.CommunityLife.Extensibility.Ranking;

/// <summary>
/// What a ranking strategy is given about one event.
/// <para>
/// Flat primitives rather than the aggregate, so a strategy in another assembly does not reference
/// this module's domain — and so a strategy cannot mutate the thing it is scoring.
/// </para>
/// </summary>
public sealed record EventRankingContext
{
    public required Guid EventId { get; init; }

    public required string CategoryKey { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }

    public required int GoingCount { get; init; }

    public required int InterestedCount { get; init; }

    public required int UniqueViewCount { get; init; }

    public required bool IsFeatured { get; init; }

    public required bool IsOrganizerVerified { get; init; }

    public bool HasCoverImage { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>Distance from the search centre, when the query had one.</summary>
    public double? DistanceKm { get; init; }

    /// <summary>Now, passed in rather than read, so a strategy is deterministic and testable.</summary>
    public required DateTimeOffset EvaluatedAt { get; init; }
}
