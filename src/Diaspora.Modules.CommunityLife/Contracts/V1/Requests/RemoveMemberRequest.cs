namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>Remove a member from an organization.</summary>
public sealed record RemoveMemberRequest
{
    /// <summary>Kept in the organization's own record; never published.</summary>
    public string? Reason { get; init; }
}
