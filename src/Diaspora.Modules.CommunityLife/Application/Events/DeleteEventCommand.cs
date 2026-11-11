using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.CommunityLife.Application.Events;

/// <summary>Soft-deletes an event.</summary>
[RequirePermission(CommunityLifePermissions.EventDeleteOwn)]
[Auditable]
public sealed record DeleteEventCommand : ICommand
{
    public required Guid EventId { get; init; }
}
