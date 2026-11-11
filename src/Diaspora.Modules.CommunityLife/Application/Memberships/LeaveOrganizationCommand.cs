using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>The caller walks away from an organization.</summary>
[RequireAuthentication(
    "Leaving the caller's own membership. Nobody needs a permission to stop belonging to " +
    "something.")]
[Auditable]
public sealed record LeaveOrganizationCommand : ICommand
{
    public required Guid OrganizationId { get; init; }
}
