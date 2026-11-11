using Diaspora.Application.Abstractions;
using Diaspora.Modules.Jobs.Contracts.V1.Requests;

namespace Diaspora.Modules.Jobs.Application.Saved;

/// <summary>
/// Saves a job to come back to. Idempotent — saving twice updates the note rather than failing,
/// because the bookmark control is tapped from a scrolling list on a connection that drops.
/// </summary>
[RequirePermission(JobPermissions.SavedManage)]
public sealed record SaveJobCommand : ICommand
{
    public required Guid JobId { get; init; }

    public required SaveJobRequest Request { get; init; }
}
