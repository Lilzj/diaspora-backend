using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Jobs.Application.Postings;

/// <summary>Retires a posting from every public surface. The applications against it stay.</summary>
[RequirePermission(JobPermissions.PostArchive)]
[Auditable]
public sealed record ArchiveJobCommand : ICommand
{
    public required Guid JobId { get; init; }
}
