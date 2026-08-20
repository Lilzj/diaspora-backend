namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Say whether you are coming.</summary>
public sealed record SubmitRsvpRequest
{
    /// <summary>
    /// <c>Interested</c> or <c>Going</c>. A full event promotes a <c>Going</c> to the waiting list
    /// rather than refusing it, which keeps both the member and the demand signal.
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// How many are coming, including you. "Me and three others" is how a family responds to a
    /// naming ceremony, and counting heads one at a time would tell an organizer to lay out a
    /// quarter of the chairs they need.
    /// </summary>
    public int PartySize { get; init; } = 1;

    /// <summary>What to tell the organizer — dietary needs, arriving late, bringing a cake.</summary>
    public string? Note { get; init; }
}
