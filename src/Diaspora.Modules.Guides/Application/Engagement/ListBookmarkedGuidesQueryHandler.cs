using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Engagement;

/// <summary>
/// Serves the member's saved guides.
/// <para>
/// <b>Community publishes no per-type "list my saved guides" surface</b> — only save, unsave and
/// the batched membership filter — and <c>GuideBookmarkService</c> says so in as many words. So
/// this list is assembled the way that surface allows: take a candidate page of guides from the
/// repository, ask Community which of them the member saved, keep those, and repeat until the
/// page is full or the sampling budget is spent.
/// </para>
/// <para>
/// <b>The consequence is worth stating plainly rather than hiding.</b> The list is ordered by
/// publication, not by when the member saved each guide, and a member who saved something very
/// old may need to page for it. The alternative — a guide-side bookmark table — would strand
/// guides outside Community's collections and make the member's saved screen join two stores,
/// which is a worse trade than a paging quirk. The budget bounds the cost either way, and there
/// is exactly one method to change when Community publishes a real listing.
/// </para>
/// </summary>
public sealed class ListBookmarkedGuidesQueryHandler
    : IQueryHandler<ListBookmarkedGuidesQuery, CursorPagedResult<GuideCardResponse>>
{
    /// <summary>Candidate pages examined per request. Bounds the cost of a sparse answer.</summary>
    private const int MaxCandidatePages = 10;

    private readonly IGuideRepository _guides;
    private readonly GuideBookmarkService _bookmarks;
    private readonly GuideAuthorizationService _authorization;
    private readonly GuideAuthorNames _authorNames;

    public ListBookmarkedGuidesQueryHandler(
        IGuideRepository guides,
        GuideBookmarkService bookmarks,
        GuideAuthorizationService authorization,
        GuideAuthorNames authorNames)
    {
        _guides = guides;
        _bookmarks = bookmarks;
        _authorization = authorization;
        _authorNames = authorNames;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        ListBookmarkedGuidesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CursorPagedResult<GuideCardResponse>>(caller.Error);
        }

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Published],
            ViewerId = caller.Value,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = GuideSortOrder.Newest,
            Limit = GuideLimits.MaxPageSize
        };

        var saved = new List<Guide>(limit);
        var cursor = query.Cursor;
        var hasMore = false;

        for (var page = 0; page < MaxCandidatePages && saved.Count < limit; page++)
        {
            var candidates = await _guides.QueryAsync(
                filter, cursor, GuideLimits.MaxPageSize, cancellationToken);

            cursor = candidates.NextCursor;
            hasMore = candidates.HasMore;

            if (candidates.Items.Count > 0)
            {
                var savedIds = await _bookmarks.FilterSavedAsync(
                    caller.Value,
                    candidates.Items.Select(guide => guide.Id).ToList(),
                    cancellationToken);

                saved.AddRange(candidates.Items
                    .Where(guide => savedIds.Contains(guide.Id.Value))
                    .Take(limit - saved.Count));
            }

            if (!hasMore || cursor is null)
            {
                break;
            }
        }

        return Result.Success(CursorPagedResult<GuideCardResponse>.Create(
            await _authorNames.ToCardsAsync(saved, cancellationToken),
            hasMore ? cursor : null,
            hasMore));
    }
}
