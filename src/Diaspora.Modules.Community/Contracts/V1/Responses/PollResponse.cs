namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A poll.</summary>
public sealed record PollResponse
{
    public required Guid PollId { get; init; }

    public required string Question { get; init; }

    public required IReadOnlyList<PollOptionResponse> Options { get; init; }

    public required bool AllowsMultiple { get; init; }

    public required string ResultVisibility { get; init; }

    public required string Status { get; init; }

    public DateTimeOffset? ClosesAt { get; init; }

    /// <summary>Null when results are hidden from this reader.</summary>
    public int? TotalVotes { get; init; }

    public int? VoterCount { get; init; }

    public required bool HasVoted { get; init; }

    public required bool CanSeeResults { get; init; }
}
