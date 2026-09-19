namespace Diaspora.Contracts.Identity.V1.Requests;

/// <summary>Gives a member a platform role, or takes one away.</summary>
public sealed record AssignRoleRequest
{
    /// <summary>
    /// A <c>UserRole</c> name: <c>Guest</c>, <c>CommunityUser</c>, <c>ServiceProvider</c>,
    /// <c>Moderator</c>, <c>Admin</c>, <c>SuperAdmin</c>.
    /// <para>
    /// A name the platform does not recognise is refused rather than defaulted. Defaulting
    /// would make a typo a demotion.
    /// </para>
    /// </summary>
    public required string Role { get; init; }

    /// <summary>Why. Recorded in the member's security history alongside who did it.</summary>
    public string? Reason { get; init; }
}

/// <summary>Signs a member out of every device.</summary>
public sealed record RevokeMemberSessionsRequest
{
    /// <summary>Why. Recorded in the member's security history.</summary>
    public string? Reason { get; init; }
}
