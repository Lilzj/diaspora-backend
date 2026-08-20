using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Mapping;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Authoring;

/// <summary>
/// Serves the author's workspace.
/// <para>
/// The caller is required and then <em>pinned as the author filter</em> — the list is by
/// definition the caller's own, and a staff caller sees their own guides here like anybody else.
/// Moderating somebody else's work is what the moderation queue is for.
/// </para>
/// <para>
/// Ordered by last edit rather than by publication, because half of this list has never been
/// published and a workspace ordered on a column that is null for every draft is a workspace in
/// arbitrary order.
/// </para>
/// </summary>
public sealed class GetMyGuidesQueryHandler
    : IQueryHandler<GetMyGuidesQuery, CursorPagedResult<GuideCardResponse>>
{
    private readonly IGuideRepository _guides;
    private readonly GuideAuthorizationService _authorization;

    public GetMyGuidesQueryHandler(
        IGuideRepository guides, GuideAuthorizationService authorization)
    {
        _guides = guides;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<GuideCardResponse>>> Handle(
        GetMyGuidesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<CursorPagedResult<GuideCardResponse>>(caller.Error);
        }

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var statuses = ParseStatuses(query.Status);

        var filter = new GuideQueryFilter
        {
            Statuses = statuses,
            AuthorId = caller.Value,
            ViewerId = caller.Value,
            ViewerIsStaff = _authorization.IsStaff,
            Sort = query.Sort is null
                ? GuideSortOrder.RecentlyUpdated
                : GuideWireNames.ParseSort(query.Sort),
            Limit = limit
        };

        var page = await _guides.QueryAsync(filter, query.Cursor, limit, cancellationToken);

        return Result.Success(CursorPagedResult<GuideCardResponse>.Create(
            page.Items.Select(GuideCardMapper.ToCard).ToList(),
            page.NextCursor,
            page.HasMore));
    }

    /// <summary>
    /// The statuses asked for, or all of them. An unrecognised name widens rather than fails —
    /// a workspace that errors over a stale filter chip is worse than one showing everything.
    /// </summary>
    private static GuideStatus[] ParseStatuses(string? status) =>
        Enum.TryParse<GuideStatus>(status?.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
            ? [parsed]
            : [GuideStatus.Draft, GuideStatus.Published, GuideStatus.Archived];
}
