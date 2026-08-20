using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Extensibility.Search;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Discovery;

/// <summary>
/// One page of official notices.
/// <para>
/// <b>The role decides what the filter is allowed to ask for, and the repository enforces it.</b>
/// Statuses and archived rows are editor-only, and rather than refusing a reader who asks for them
/// the query simply does not pass them on — a browse screen that sent a stale filter should get a
/// page, not a 403.
/// </para>
/// </summary>
public sealed class ListOfficialNoticesQueryHandler
    : IQueryHandler<ListOfficialNoticesQuery, CursorPagedResult<OfficialNoticeCardResponse>>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly IntelligenceReferenceResolver _references;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IClock _clock;

    public ListOfficialNoticesQueryHandler(
        IOfficialNoticeRepository notices,
        IntelligenceReferenceResolver references,
        IntelligenceAuthorizationService authorization,
        IClock clock)
    {
        _notices = notices;
        _references = references;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<CursorPagedResult<OfficialNoticeCardResponse>>> Handle(
        ListOfficialNoticesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var isEditor = _authorization.IsEditor;
        var now = _clock.UtcNow;

        var filter = new NoticeQueryFilter
        {
            CountryCode = query.CountryCode,
            CategoryKey = query.CategoryKey,
            AuthorityId = query.AuthorityId is { } id ? IssuingAuthorityId.From(id) : null,
            Severities = ParseSeverities(query.Severities),
            MinimumSeverity = IntelligenceWireNames.ParseSeverity(query.MinimumSeverity),
            Language = query.Language,
            Text = query.Text,
            OfficialOnly = query.OfficialOnly,
            ActiveOnly = query.ActiveOnly,
            ExpiredOnly = query.ExpiredOnly,
            AsOf = now,
            Statuses = ParseStatuses(query.Statuses),
            ViewerIsEditor = isEditor,
            IncludeArchived = isEditor && query.IncludeArchived,
            Sort = IntelligenceWireNames.ParseSort(query.Sort),
            Limit = query.Limit ?? IntelligenceLimits.DefaultPageSize
        };

        var page = await _notices.ListAsync(filter, query.Cursor, cancellationToken);

        var references = await _references.ResolveAsync(page.Items, cancellationToken);

        return Result.Success(
            CursorPagedResult<OfficialNoticeCardResponse>.Create(
                [
                    .. page.Items.Select(notice => OfficialNoticeMapper.ToCard(
                        notice,
                        references.Category(notice.CategoryKey),
                        references.Authority(notice.AuthorityId),
                        now,
                        query.Locale))
                ],
                page.NextCursor,
                page.HasMore));
    }

    /// <summary>
    /// Names to enum values, dropping anything unrecognised.
    /// <para>
    /// Dropping widens the answer, which is the safe direction for a filter on official information:
    /// an old app version sending a severity this deployment removed should see more than it expected
    /// rather than nothing at all.
    /// </para>
    /// </summary>
    private static IReadOnlyCollection<NoticeSeverity> ParseSeverities(
        IReadOnlyList<string> names) =>
    [
        .. names
            .Select(IntelligenceWireNames.ParseSeverity)
            .OfType<NoticeSeverity>()
            .Distinct()
    ];

    /// <summary>
    /// Names to statuses, falling back to the reader default when nothing usable was given.
    /// <para>
    /// The repository narrows this again for a non-editor, so a reader sending
    /// <c>?status=Draft</c> gets published notices rather than an empty page or an error.
    /// </para>
    /// </summary>
    private static List<NoticeStatus> ParseStatuses(IReadOnlyList<string> names)
    {
        var parsed = names
            .Select(IntelligenceWireNames.ParseStatus)
            .OfType<NoticeStatus>()
            .Distinct()
            .ToList();

        return parsed.Count == 0
            ? [NoticeStatus.Published, NoticeStatus.Superseded]
            : parsed;
    }
}

/// <summary>
/// One official notice in full.
/// <para>
/// <b>No view is recorded here.</b> Counting on the read would count every prefetch, every crawler
/// and every retry, and would make this endpoint un-cacheable. The client says explicitly that
/// somebody looked, through <c>RecordNoticeActivityCommand</c>.
/// </para>
/// </summary>
public sealed class GetOfficialNoticeQueryHandler
    : IQueryHandler<GetOfficialNoticeQuery, OfficialNoticeResponse>
{
    private readonly IOfficialNoticeRepository _notices;
    private readonly OfficialNoticePresenter _presenter;
    private readonly IntelligenceAuthorizationService _authorization;

    public GetOfficialNoticeQueryHandler(
        IOfficialNoticeRepository notices,
        OfficialNoticePresenter presenter,
        IntelligenceAuthorizationService authorization)
    {
        _notices = notices;
        _presenter = presenter;
        _authorization = authorization;
    }

    public async Task<Result<OfficialNoticeResponse>> Handle(
        GetOfficialNoticeQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var isEditor = _authorization.IsEditor;

        // An editor may read an archived notice; a reader may not know it exists. Reading past the
        // filter only for an editor is what keeps a 404 from becoming an existence oracle.
        var noticeId = OfficialNoticeId.From(query.OfficialNoticeId);

        var notice = isEditor
            ? await _notices.GetIncludingArchivedAsync(noticeId, cancellationToken)
            : await _notices.GetAsync(noticeId, cancellationToken);

        if (notice is null)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.NoticeNotFound);
        }

        if (!isEditor && !notice.IsReaderVisible)
        {
            return Result.Failure<OfficialNoticeResponse>(IntelligenceErrors.NoticeNotFound);
        }

        return Result.Success(
            await _presenter.PresentAsync(notice, isEditor, query.Locale, cancellationToken));
    }
}

/// <summary>
/// Official notice search.
/// <para>
/// <b>The engine returns identifiers and this resolves them against the write model.</b> An external
/// index a day behind can hand back the identifier of a notice superseded this morning, and the
/// reader still sees the current truth — which is the whole reason a hit carries nothing but an
/// identifier and a score.
/// </para>
/// <para>
/// <b>An unavailable engine is a service-unavailable failure, never an empty page.</b> On official
/// information those two answers mean opposite things, and the wrong one is the more reassuring.
/// </para>
/// </summary>
public sealed class SearchOfficialNoticesQueryHandler
    : IQueryHandler<SearchOfficialNoticesQuery, OfficialNoticeSearchResponse>
{
    private readonly IOfficialNoticeSearchEngine _engine;
    private readonly IOfficialNoticeRepository _notices;
    private readonly IntelligenceReferenceResolver _references;
    private readonly IntelligenceAnalyticsRecorder _analytics;
    private readonly IClock _clock;

    public SearchOfficialNoticesQueryHandler(
        IOfficialNoticeSearchEngine engine,
        IOfficialNoticeRepository notices,
        IntelligenceReferenceResolver references,
        IntelligenceAnalyticsRecorder analytics,
        IClock clock)
    {
        _engine = engine;
        _notices = notices;
        _references = references;
        _analytics = analytics;
        _clock = clock;
    }

    public async Task<Result<OfficialNoticeSearchResponse>> Handle(
        SearchOfficialNoticesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Text is { Length: > IntelligenceLimits.MaxSearchTerm })
        {
            return Result.Failure<OfficialNoticeSearchResponse>(
                IntelligenceErrors.SearchTermInvalid);
        }

        if (!await _engine.IsAvailableAsync(cancellationToken))
        {
            return Result.Failure<OfficialNoticeSearchResponse>(
                IntelligenceErrors.SearchUnavailable);
        }

        var outcome = await _engine.SearchAsync(
            new NoticeSearchRequest
            {
                Text = query.Text,
                CountryCode = IntelligenceNormalization.Country(query.CountryCode),
                CategoryKeys = [.. query.CategoryKeys],
                AuthorityId = query.AuthorityId?.ToString(),
                Severities = [.. query.Severities],
                Language = query.Language,
                OfficialOnly = query.OfficialOnly,
                ActiveOnly = query.ActiveOnly,
                ExpiredOnly = query.ExpiredOnly,
                PublishedFrom = query.PublishedFrom?.ToString("O"),
                EffectiveFrom = query.EffectiveFrom?.ToString("O"),
                Sort = query.Sort,
                Cursor = query.Cursor,
                Limit = Math.Clamp(
                    query.Limit ?? IntelligenceLimits.DefaultPageSize,
                    1,
                    IntelligenceLimits.MaxPageSize)
            },
            cancellationToken);

        var notices = await _notices.GetByIdsAsync(
            [.. outcome.Hits.Select(hit => OfficialNoticeId.From(hit.OfficialNoticeId))],
            cancellationToken);

        var references = await _references.ResolveAsync(notices, cancellationToken);
        var now = _clock.UtcNow;

        // The engine's order is the answer, so the resolved notices are re-sorted into it. An
        // identifier the engine returned that no longer resolves — superseded and hard-removed, or
        // an index that is behind — is dropped rather than rendered as a gap.
        var byId = notices.ToDictionary(notice => notice.Id.Value);

        var cards = outcome.Hits
            .Select(hit => byId.TryGetValue(hit.OfficialNoticeId, out var notice) ? notice : null)
            .OfType<OfficialNotice>()
            .Select(notice => OfficialNoticeMapper.ToCard(
                notice,
                references.Category(notice.CategoryKey),
                references.Authority(notice.AuthorityId),
                now,
                query.Locale))
            .ToList();

        await _analytics.RecordAsync(
            _analytics.Signal(
                IntelligenceAnalyticsSignalKinds.Searched,
                IntelligenceNormalization.Country(query.CountryCode),
                term: query.Text,
                resultCount: cards.Count,
                locale: query.Locale),
            cancellationToken);

        return Result.Success(
            new OfficialNoticeSearchResponse
            {
                Results = cards,
                NextCursor = outcome.NextCursor,
                HasMore = outcome.HasMore,
                TotalCount = outcome.TotalCount,
                Engine = outcome.Engine,
                Query = query.Text
            });
    }
}
