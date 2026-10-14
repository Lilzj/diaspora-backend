using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Discovery;

/// <summary>
/// Serves an author's public shelf.
/// <para>
/// The status filter is pinned to published here rather than left to the audience predicates:
/// the repository would happily include the caller's own drafts when the caller <em>is</em> the
/// author, and a byline page that quietly grew half-written guides the moment its owner looked
/// at it would be a confusing surface to reason about — and a confusing one to screenshot.
/// </para>
/// </summary>
public sealed class ListGuidesByAuthorQueryHandler
    : IQueryHandler<ListGuidesByAuthorQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideAuthorNames _authorNames;

    public ListGuidesByAuthorQueryHandler(
        IGuideRepository guides, GuideAuthorizationService authorization,
        GuideAuthorNames authorNames)
    {
        _guides = guides;
        _authorization = authorization;
        _authorNames = authorNames;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        ListGuidesByAuthorQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            AuthorId = UserId.From(query.AuthorId),
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = GuideWireNames.ParseSort(query.Sort),
            Limit = limit
        };

        var page = await _guides.QueryAsync(filter, query.Cursor, limit, cancellationToken);

        return Result.Success(CursorPagedResult<GuideCardResponse>.Create(
            await _authorNames.ToCardsAsync(page.Items, cancellationToken),
            page.NextCursor,
            page.HasMore));
    }
}
