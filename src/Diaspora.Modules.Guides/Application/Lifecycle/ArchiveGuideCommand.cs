using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Lifecycle;

/// <summary>
/// Retires a published guide from every reading surface.
/// <para>
/// Archiving is the honest end for advice that has gone stale — a visa procedure that changed,
/// a bank that closed. It is not a delete: the versions stay, the deep link keeps resolving for
/// the author, and restoring puts the guide back exactly as it was.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.ArchiveOwn)]
[Auditable]
public sealed record ArchiveGuideCommand : ICommand<GuideResponse>
{
    public required Guid GuideId { get; init; }
}
