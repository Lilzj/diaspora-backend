using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Identity.V1.Events;

/// <summary>
/// A member's platform role changed (7 Nov 2026), with the reason staff gave. Published so the
/// member is told; a role used to change in silence.
/// </summary>
public sealed record UserRoleChangedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    /// <summary>A <c>UserRole</c> name, for example <c>CommunityUser</c>.</summary>
    public required string PreviousRole { get; init; }

    /// <summary>A <c>UserRole</c> name, for example <c>Moderator</c>.</summary>
    public required string NewRole { get; init; }

    /// <summary>Why, in the words of the member of staff who changed it.</summary>
    public string? Reason { get; init; }
}
