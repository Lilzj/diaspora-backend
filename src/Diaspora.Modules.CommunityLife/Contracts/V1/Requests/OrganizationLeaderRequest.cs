namespace Diaspora.Modules.CommunityLife.Contracts.V1.Requests;

/// <summary>One person named on an organization page.</summary>
public sealed record OrganizationLeaderRequest
{
    public required string Name { get; init; }

    /// <summary>What they are called in the organization: Pastor, Imam, Chair, Secretary.</summary>
    public string? Role { get; init; }

    /// <summary>Their platform account, when they have one. Optional by design.</summary>
    public Guid? MemberUserId { get; init; }

    /// <summary>A B5 asset identifier. Never a URL.</summary>
    public Guid? PhotoMediaId { get; init; }
}
