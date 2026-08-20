using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Suggestions;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Moderation;

/// <summary>
/// Serves the moderation queue.
/// <para>
/// <b>Staff is checked here as well as by the permission, and the check is not redundant.</b>
/// The permission says the caller may moderate; the audience flag on the filter is what makes
/// hidden guides visible to the query at all, and setting it from anything other than a
/// confirmed staff role would hand a moderation view to whoever the permission catalogue
/// accidentally granted.
/// </para>
/// <para>
/// <b>Two bounded compromises, both in the same shape as the rest of this module's read
/// path.</b> <c>GuideQueryFilter</c> publishes no moderation-state predicate, so the queue is
/// selected by paging staff-visible guides and keeping the non-clear ones; and open-suggestion
/// counts come from one bounded read of the open-suggestion feed, grouped by guide, rather than
/// from a count query per row. Both leave exactly one place to improve when persistence grows
/// the predicate and the aggregate.
/// </para>
/// </summary>
public sealed class ListModerationQueueQueryHandler
    : IQueryHandler<ListModerationQueueQuery, CursorPagedResult<GuideModerationQueueItemResponse>>
{
    /// <summary>Candidate pages examined per request. Bounds the cost of a quiet queue.</summary>
    private const int MaxCandidatePages = 10;

    /// <summary>Open suggestions sampled for the per-guide counts.</summary>
    private const int SuggestionSample = 200;

    private readonly IGuideRepository _guides;
    private readonly IGuideSuggestionRepository _suggestions;
    private readonly GuideAuthorizationService _authorization;

    public ListModerationQueueQueryHandler(
        IGuideRepository guides,
        IGuideSuggestionRepository suggestions,
        GuideAuthorizationService authorization)
    {
        _guides = guides;
        _suggestions = suggestions;
        _authorization = authorization;
    }

    public async Task<Result<CursorPagedResult<GuideModerationQueueItemResponse>>> Handle(
        ListModerationQueueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (!_authorization.IsStaff)
        {
            return Result.Failure<CursorPagedResult<GuideModerationQueueItemResponse>>(
                GuideErrors.NotModerator);
        }

        var limit = Math.Clamp(
            query.Limit ?? GuideLimits.DefaultPageSize, 1, GuideLimits.MaxPageSize);

        var wanted = ParseState(query.ModerationState);

        var filter = new GuideQueryFilter
        {
            Statuses = [GuideStatus.Draft, GuideStatus.Published, GuideStatus.Archived],
            CountryCode = string.IsNullOrWhiteSpace(query.CountryCode)
                ? null
                : query.CountryCode.Trim().ToUpperInvariant(),
            ViewerId = _authorization.CallerId,
            ViewerIsStaff = true,
            Sort = GuideSortOrder.RecentlyUpdated,
            Limit = GuideLimits.MaxPageSize
        };

        var queued = new List<Guide>(limit);
        var cursor = query.Cursor;
        var hasMore = false;

        for (var page = 0; page < MaxCandidatePages && queued.Count < limit; page++)
        {
            var candidates = await _guides.QueryAsync(
                filter, cursor, GuideLimits.MaxPageSize, cancellationToken);

            cursor = candidates.NextCursor;
            hasMore = candidates.HasMore;

            queued.AddRange(candidates.Items
                .Where(guide => Matches(guide, wanted))
                .Take(limit - queued.Count));

            if (!hasMore || cursor is null)
            {
                break;
            }
        }

        var openCounts = await CountOpenSuggestionsAsync(cancellationToken);

        var items = queued
            .Select(guide => new GuideModerationQueueItemResponse
            {
                GuideId = guide.Id.Value,
                Slug = guide.Slug.Value,
                Title = guide.Title.Value,
                AuthorId = guide.AuthorId.Value,
                Status = GuideWireNames.ToWire(guide.Status),
                ModerationState = GuideWireNames.ToWire(guide.ModerationState),
                CountryCode = guide.Location.CountryCode,
                CategoryKey = guide.CategoryKey,
                OpenSuggestions = openCounts.GetValueOrDefault(guide.Id.Value),
                UpdatedAt = guide.ModifiedAt
            })
            .ToList();

        return Result.Success(CursorPagedResult<GuideModerationQueueItemResponse>.Create(
            items, hasMore ? cursor : null, hasMore));
    }

    /// <summary>
    /// Open suggestions per guide, over the oldest sampled window — the ones that have waited
    /// longest, which is what a queue is for. A guide outside the window reports zero rather
    /// than a wrong number.
    /// </summary>
    private async Task<Dictionary<Guid, int>> CountOpenSuggestionsAsync(
        CancellationToken cancellationToken)
    {
        var open = await _suggestions.ListOpenAsync(
            cursor: null, SuggestionSample, cancellationToken);

        var counts = new Dictionary<Guid, int>();

        foreach (var suggestion in open.Items)
        {
            var key = suggestion.GuideId.Value;
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }

        return counts;
    }

    /// <summary>Whether a candidate belongs in the queue at all, under the requested narrowing.</summary>
    private static bool Matches(Guide guide, GuideModerationState? wanted) =>
        wanted is { } state
            ? guide.ModerationState == state
            : guide.ModerationState != GuideModerationState.Clear;

    /// <summary>
    /// The requested state, or null for "everything that is not clear". An unrecognised name
    /// widens rather than fails — a stale filter chip should not empty a moderator's queue.
    /// </summary>
    private static GuideModerationState? ParseState(string? value) =>
        Enum.TryParse<GuideModerationState>(value?.Trim(), ignoreCase: true, out var parsed)
        && Enum.IsDefined(parsed)
        && parsed != GuideModerationState.Clear
            ? parsed
            : null;
}
