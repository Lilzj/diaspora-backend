using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// The author's own workspace — every guide they have written, in whatever state.
/// <para>
/// Distinct from the public byline shelf: this one carries drafts, archived guides and guides
/// moderation has hidden, because an author locked out of their own hidden guide cannot fix
/// whatever got it hidden. Permissioned rather than public for exactly that reason — the list
/// has an audience of one.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.ViewDrafts)]
public sealed record GetMyGuidesQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    /// <summary>A <c>GuideStatus</c> name to narrow to. Null returns every state.</summary>
    public string? Status { get; init; }

    /// <summary>A sort key. Unknown values fall back to newest.</summary>
    public string? Sort { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
