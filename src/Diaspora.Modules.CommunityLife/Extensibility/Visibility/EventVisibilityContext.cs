using Diaspora.Modules.CommunityLife.Domain.Enums;

namespace Diaspora.Modules.CommunityLife.Extensibility.Visibility;

/// <summary>What a visibility policy is given about a reader and an event.</summary>
public sealed record EventVisibilityContext
{
    public required Guid EventId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required Guid OrganizerUserId { get; init; }

    public required EventVisibility Visibility { get; init; }

    public required EventStatus Status { get; init; }

    public required CommunityLifeModerationState ModerationState { get; init; }

    /// <summary>The reader. Null for somebody who is not signed in.</summary>
    public Guid? ViewerUserId { get; init; }

    /// <summary>The reader's role in the organizing organization, when they have one.</summary>
    public MembershipRole? ViewerMembershipRole { get; init; }

    public required bool ViewerIsStaff { get; init; }
}
