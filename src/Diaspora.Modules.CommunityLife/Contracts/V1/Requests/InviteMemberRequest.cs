namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Invite somebody into an organization.</summary>
public sealed record InviteMemberRequest
{
    public required Guid MemberUserId { get; init; }

    /// <summary>
    /// The role to grant on acceptance. Cannot exceed the inviter's own — the handler refuses,
    /// because an administrator who could mint an owner could take an organization from its founder.
    /// </summary>
    public string? Role { get; init; }

    public string? Message { get; init; }
}
