namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>One member's standing in one organization.</summary>
public sealed record MembershipResponse
{
    public required Guid MembershipId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid MemberUserId { get; init; }

    public required string Status { get; init; }

    public required string Role { get; init; }

    /// <summary>Read by whoever decides, and cleared once the membership ends.</summary>
    public string? RequestMessage { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? JoinedAt { get; init; }

    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>When it lapses if not renewed. Null on nearly every membership.</summary>
    public DateTimeOffset? RenewsAt { get; init; }

    /// <summary>Events attended under this organization. What a volunteer roster sorts by.</summary>
    public required int AttendedEventCount { get; init; }

    public required int Revision { get; init; }
}
