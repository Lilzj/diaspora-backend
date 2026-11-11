using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.Knowledge.LearningPaths;

/// <summary>
/// Moves a learning path between draft, published and archived.
/// <para>
/// <b>One command for three transitions, and the reason is that they are the same decision made
/// three ways.</b> Three separate commands would be three handlers that each load a path, check a
/// revision, call one aggregate method and invalidate one cache — the aggregate already refuses an
/// illegal transition and returns the specific error, so splitting them would produce triplicated
/// plumbing and no additional safety.
/// </para>
/// <para>
/// The country directory commands do split, deliberately, because publishing and archiving a
/// directory genuinely differ: one flips a flag, the other goes through the platform's soft-delete
/// path and is not a state change at all.
/// </para>
/// </summary>
[RequirePermission(KnowledgePermissions.ManagePaths)]
[Auditable]
public sealed record ChangeLearningPathStatusCommand : ICommand
{
    public required string CountryCode { get; init; }

    public required string Key { get; init; }

    /// <summary>
    /// Where the path should end up. <c>Draft</c> means restoring an archived path — never
    /// un-publishing a live one, which would be a separate decision the aggregate does not offer.
    /// </summary>
    public required LearningPathStatus Status { get; init; }
}
