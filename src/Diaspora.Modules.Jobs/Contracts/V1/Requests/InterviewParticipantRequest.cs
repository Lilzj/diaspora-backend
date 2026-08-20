namespace Diaspora.Modules.Jobs.Contracts.V1.Requests;

/// <summary>
/// One interviewer.
/// <para>
/// The name is free text and <see cref="UserId"/> is optional, because a panel routinely includes
/// people who are not platform members — a ward manager, an external assessor, a client of the
/// agency. Requiring an account for each would mean employers stopped listing them, and the
/// candidate would lose the one thing that lets them prepare.
/// </para>
/// </summary>
public sealed record InterviewParticipantRequest
{
    public required string Name { get; init; }

    /// <summary>What they do — "Ward Manager", "Head of Engineering".</summary>
    public string? Role { get; init; }

    public Guid? UserId { get; init; }
}
