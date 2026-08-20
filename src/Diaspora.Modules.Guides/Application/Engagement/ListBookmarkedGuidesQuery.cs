using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// The guides the member has saved.
/// <para>
/// Permissioned and personal: a saved list is a record of what somebody is worried about — a
/// visa, a diagnosis, a tenancy dispute — and it belongs to nobody but them.
/// </para>
/// <para>
/// The storage is Community's, not this module's, because the member's saved-collections screen
/// shows guides beside providers and posts from one store with one folder model.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.Bookmark)]
public sealed record ListBookmarkedGuidesQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
