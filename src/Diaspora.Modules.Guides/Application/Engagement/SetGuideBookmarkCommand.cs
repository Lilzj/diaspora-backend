using Diaspora.Application.Abstractions;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Saves or unsaves a guide.
/// <para>
/// One command for both directions, because the client's control is a toggle, and a toggle that
/// maps to two commands has to decide which one to send — from state it may not have, on a
/// screen that may have been open for an hour. An idempotent set is the honest shape.
/// </para>
/// <para>
/// Not <c>[Transactional]</c>: the bookmark itself lands in Community's aggregate, not this
/// module's, and wrapping a call into another module's handler in this module's transaction is
/// how two modules end up sharing a unit of work neither can reason about.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.Bookmark)]
[Auditable]
public sealed record SetGuideBookmarkCommand : ICommand
{
    public required Guid GuideId { get; init; }

    public required bool IsSaved { get; init; }
}
