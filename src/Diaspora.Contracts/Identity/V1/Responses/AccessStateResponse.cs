namespace Diaspora.Contracts.Identity.V1.Responses;

/// <summary>
/// The member's position on the progressive access ladder and the routing that follows
/// from it.
/// </summary>
public sealed record AccessStateResponse
{
    public required string State { get; init; }

    public required string AccountStatus { get; init; }

    public required AccessDecisionResponse Decision { get; init; }

    /// <summary>The states reachable from here, for clients that render progress.</summary>
    public required IReadOnlyList<string> NextStates { get; init; }
}
