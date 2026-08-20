using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Series;

/// <summary>
/// A page of repeating events.
/// <para>
/// <b>"Mine" is resolved from the token, never from the query string.</b> A caller asking for their
/// own series gets their own; a caller asking for everybody's gets the published ones. There is no
/// parameter that lets one ask for another member's drafts, because there is no honest reason to.
/// </para>
/// </summary>
public sealed class SearchEventSeriesQueryHandler
    : IQueryHandler<SearchEventSeriesQuery, EventSeriesPageResponse>
{
    private readonly IEventSeriesRepository _series;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly EventSeriesReadContext _read;

    public SearchEventSeriesQueryHandler(
        IEventSeriesRepository series,
        CommunityLifeAuthorizationService authorization,
        EventSeriesReadContext read)
    {
        _series = series;
        _authorization = authorization;
        _read = read;
    }

    public async Task<Result<EventSeriesPageResponse>> Handle(
        SearchEventSeriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.MineOnly && !_authorization.IsAuthenticated)
        {
            return Result.Failure<EventSeriesPageResponse>(
                Domain.Errors.CommunityLifeErrors.NotAuthenticated);
        }

        var page = await _series.SearchAsync(
            new EventSeriesQueryFilter
            {
                SearchTerm = query.SearchTerm,
                OrganizationId = query.OrganizationId is { } organizationId
                    ? OrganizationId.From(organizationId)
                    : null,
                OrganizerUserId = query.MineOnly ? _authorization.CallerId : null,
                CategoryKeys = query.CategoryKeys,
                CountryCode = query.CountryCode,
                CityName = query.CityName,
                Statuses = ParseStatuses(query.Statuses),
                ViewerUserId = _authorization.CallerId,
                ViewerOrganizationIds = query.MineOnly
                    ? null
                    : await _authorization.CallerOrganizationsAsync(cancellationToken),

                // Only "mine" widens the listing, and staff get no exemption here — the same rule
                // the event browse follows. Granting one would have been silently wrong rather than
                // generous: the filter narrows an all-inclusive listing to the viewer's own rows, so
                // a moderator browsing every pattern on the platform would have been shown theirs.
                IncludeNonPublic = query.MineOnly,
                Cursor = query.Cursor,
                Limit = Math.Clamp(
                    query.Limit ?? CommunityLifeLimits.DefaultPageSize,
                    1,
                    CommunityLifeLimits.MaxPageSize)
            },
            cancellationToken);

        return Result.Success(new EventSeriesPageResponse
        {
            Items = await _read.ToResponsesAsync(page.Items, cancellationToken),
            NextCursor = page.NextCursor,
            HasMore = page.HasMore
        });
    }

    private static List<EventSeriesStatus>? ParseStatuses(IReadOnlyList<string>? statuses)
    {
        if (statuses is not { Count: > 0 })
        {
            return null;
        }

        var parsed = statuses
            .Select(CommunityLifeSharedMapper.ParseEnum<EventSeriesStatus>)
            .OfType<EventSeriesStatus>()
            .Distinct()
            .ToList();

        return parsed.Count == 0 ? null : parsed;
    }
}
