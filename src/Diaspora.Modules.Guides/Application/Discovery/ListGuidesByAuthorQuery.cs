using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// One author's published guides — the byline a reader clicks after a guide helped them.
/// <para>
/// Public, and published-only whoever asks. An author reading their own page here sees the same
/// list a stranger does; their drafts live behind <c>GetMyDraftsQuery</c>, which is a different
/// screen with a different audience.
/// </para>
/// </summary>
[AllowAnonymous(
    "Published reference content, and the platform's public face. Somebody researching a move " +
    "reads this before they have an account; requiring one would make the knowledge base useless " +
    "to the people it exists for.")]
public sealed record ListGuidesByAuthorQuery : IQuery<CursorPagedResult<GuideCardResponse>>
{
    public required Guid AuthorId { get; init; }

    /// <summary>A sort key. Unknown values fall back to newest.</summary>
    public string? Sort { get; init; }

    public string? Cursor { get; init; }

    public int? Limit { get; init; }
}
