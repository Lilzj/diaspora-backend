using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Moderation;

/// <summary>
/// The guides waiting for a moderator.
/// <para>
/// Staff-only, and the one read in this module that deliberately sees what nobody else does:
/// hidden guides, flagged guides, and the drafts behind them. A moderator who could not see the
/// content they are deciding about would be deciding about a title.
/// </para>
/// </summary>
[RequirePermission(GuidePermissions.Moderate)]
public sealed record ListModerationQueueQuery
    : IQuery<CursorPagedResult<GuideModerationQueueItemResponse>>
{
    /// <summary>
    /// A <c>GuideModerationState</c> name to narrow to — <c>Flagged</c> or <c>Hidden</c>. Null
    /// returns both.
    /// </summary>
    public string? ModerationState { get; init; }

    /// <summary>Narrows the queue to one country, for a regionally staffed rota.</summary>
    public string? CountryCode { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
