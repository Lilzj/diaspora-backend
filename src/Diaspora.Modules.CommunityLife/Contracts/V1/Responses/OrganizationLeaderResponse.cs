namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>Somebody named on an organization page.</summary>
public sealed record OrganizationLeaderResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Role { get; init; }

    public Guid? MemberUserId { get; init; }

    public Guid? PhotoMediaId { get; init; }

    public required int Order { get; init; }
}
