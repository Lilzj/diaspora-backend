namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>A poll attached to a post.</summary>
public sealed record PollRequest
{
    public required string Question { get; init; }

    public required IReadOnlyList<PollOptionRequest> Options { get; init; }

    public bool AllowsMultiple { get; init; }

    /// <summary><c>afterVoting</c> (default), <c>always</c> or <c>afterClosing</c>.</summary>
    public string? ResultVisibility { get; init; }

    public DateTimeOffset? ClosesAt { get; init; }
}
