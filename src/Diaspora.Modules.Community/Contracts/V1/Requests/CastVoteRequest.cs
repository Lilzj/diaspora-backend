namespace Diaspora.Modules.Community.Contracts.V1.Requests;

/// <summary>A poll vote. Several options only on a multi-choice poll.</summary>
public sealed record CastVoteRequest
{
    public required IReadOnlyList<Guid> OptionIds { get; init; }
}
