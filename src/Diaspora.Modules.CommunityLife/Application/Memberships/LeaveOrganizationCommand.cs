using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.CommunityLife.Application.Memberships;

/// <summary>The caller walks away from an organization.</summary>
[Authorize]
[Auditable]
public sealed record LeaveOrganizationCommand : ICommand
{
    public required Guid OrganizationId { get; init; }
}
