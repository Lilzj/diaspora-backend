using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Jobs.Application.Saved;

/// <summary>Removes a job from the caller's saved list. Idempotent.</summary>
[RequirePermission(JobPermissions.SavedManage)]
public sealed record UnsaveJobCommand : ICommand
{
    public required Guid JobId { get; init; }
}
