namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Change a member's role inside an organization.</summary>
public sealed record ChangeMemberRoleRequest
{
    /// <summary>
    /// <c>Member</c>, <c>Volunteer</c>, <c>Moderator</c>, <c>Organizer</c>, <c>Administrator</c>,
    /// <c>Owner</c>. Granting a role above your own is refused.
    /// </summary>
    public required string Role { get; init; }
}
