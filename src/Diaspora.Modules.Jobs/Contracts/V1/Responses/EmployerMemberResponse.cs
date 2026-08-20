namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>One member of an employer's team.</summary>
public sealed record EmployerMemberResponse
{
    public required Guid UserId { get; init; }

    /// <summary><c>Viewer</c>, <c>Recruiter</c>, <c>Administrator</c> or <c>Owner</c>.</summary>
    public required string Role { get; init; }

    public required DateTimeOffset JoinedAt { get; init; }
}
