using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Extensibility.Membership;

/// <summary>What a membership policy provider is given about somebody asking to join.</summary>
public sealed record MembershipDecisionContext
{
    public required Guid OrganizationId { get; init; }

    public required string OrganizationTypeKey { get; init; }

    public required Guid ApplicantUserId { get; init; }

    public required MembershipPolicyKind PolicyKind { get; init; }

    /// <summary>What the organization asks of its members, in its own words.</summary>
    public string? Requirements { get; init; }

    /// <summary>What the applicant said. The only thing an administrator would otherwise read.</summary>
    public string? RequestMessage { get; init; }

    /// <summary>Whether the applicant has ever belonged before, and how it ended.</summary>
    public MembershipStatus? PreviousStatus { get; init; }

    public required int CurrentMemberCount { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }
}
