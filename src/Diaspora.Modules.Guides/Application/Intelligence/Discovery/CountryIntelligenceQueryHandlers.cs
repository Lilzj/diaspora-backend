using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.IntelligenceAnalytics;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Modules.Guides.Extensibility.Caching;
using Diaspora.Modules.Guides.Extensibility.Intelligence;
using Diaspora.Modules.Guides.Extensibility.Search;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Application.Intelligence.Discovery;

/// <summary>
/// Serves a country's whole intelligence layer.
/// <para>
/// <b>Cached, and on a shorter window than the knowledge dashboard.</b> A country's guides change
/// weekly; a country's emergency alerts change during an emergency. Invalidation on publication is
/// exact — the relay drops the country's cache on every notice change — so this window only ever
/// covers what invalidation cannot see, and two minutes of that is about the most a reader should
/// wear on a page that may be telling them a border has closed.
/// </para>
/// <para>
/// <b>An editor's view is never cached.</b> They read it immediately after a write, and a cache would
/// show them their own publication not having happened.
/// </para>
/// </summary>
public sealed class GetCountryIntelligenceQueryHandler
    : IQueryHandler<GetCountryIntelligenceQuery, CountryIntelligenceResponse>
{
    private readonly CountryIntelligenceComposer _composer;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IntelligenceAnalyticsRecorder _analytics;
    private readonly IKnowledgeCache _cache;
    private readonly IOptions<IntelligenceOptions> _options;

    public GetCountryIntelligenceQueryHandler(
        CountryIntelligenceComposer composer,
        IntelligenceAuthorizationService authorization,
        IntelligenceAnalyticsRecorder analytics,
        IKnowledgeCache cache,
        IOptions<IntelligenceOptions> options)
    {
        _composer = composer;
        _authorization = authorization;
        _analytics = analytics;
        _cache = cache;
        _options = options;
    }

    public async Task<Result<CountryIntelligenceResponse>> Handle(
        GetCountryIntelligenceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var country = IntelligenceNormalization.Country(query.CountryCode);

        if (country is null)
        {
            return Result.Failure<CountryIntelligenceResponse>(
                IntelligenceErrors.CountryInvalid);
        }

        if (query.RecordView)
        {
            await _analytics.RecordAsync(
                _analytics.Signal(
                    IntelligenceAnalyticsSignalKinds.DashboardViewed,
                    country,
                    locale: query.Locale),
                cancellationToken);
        }

        var cacheable = _options.Value.EnableCaching
            && _cache.IsEnabled
            && !_authorization.IsEditor;

        if (!cacheable)
        {
            return Result.Success(
                await _composer.ComposeAsync(country, query.Locale, cancellationToken));
        }

        var key = IntelligenceCacheKeys.Country(country, query.Locale);

        var cached = await _cache.GetAsync<CountryIntelligenceResponse>(key, cancellationToken);

        if (cached is not null)
        {
            return Result.Success(cached);
        }

        var composed = await _composer.ComposeAsync(country, query.Locale, cancellationToken);

        await _cache.SetAsync(
            key, composed, _options.Value.CacheDuration, cancellationToken);

        return Result.Success(composed);
    }
}

/// <summary>
/// What this deployment's Country Intelligence Platform can actually do.
/// <para>
/// Uncached, because it is cheap and because it is the endpoint a client calls when something has
/// gone wrong — an operator asking "is search up" served a two-minute-old answer is being told about
/// a problem they have already had time to fix.
/// </para>
/// </summary>
public sealed class GetIntelligenceCapabilitiesQueryHandler
    : IQueryHandler<GetIntelligenceCapabilitiesQuery, IntelligenceCapabilitiesResponse>
{
    private readonly INoticeCategoryRepository _categories;
    private readonly IOfficialNoticeSearchEngine _engine;
    private readonly AuthorityVerificationProviderRegistry _verification;
    private readonly IntelligenceSourceValidator _sources;
    private readonly IntelligenceAnalyticsRecorder _analytics;
    private readonly IReadOnlyList<IEmergencyContactKindContributor> _kinds;
    private readonly IOptions<IntelligenceOptions> _options;

    public GetIntelligenceCapabilitiesQueryHandler(
        INoticeCategoryRepository categories,
        IOfficialNoticeSearchEngine engine,
        AuthorityVerificationProviderRegistry verification,
        IntelligenceSourceValidator sources,
        IntelligenceAnalyticsRecorder analytics,
        IEnumerable<IEmergencyContactKindContributor> kinds,
        IOptions<IntelligenceOptions> options)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        _categories = categories;
        _engine = engine;
        _verification = verification;
        _sources = sources;
        _analytics = analytics;
        _kinds = [.. kinds.OrderBy(contributor => contributor.Order)];
        _options = options;
    }

    public async Task<Result<IntelligenceCapabilitiesResponse>> Handle(
        GetIntelligenceCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var categories = await _categories.ListAsync(
            includeArchived: false, cancellationToken);

        return Result.Success(
            new IntelligenceCapabilitiesResponse
            {
                Categories =
                [
                    .. categories.Select(category =>
                        IntelligenceCatalogueMapper.ToResponse(category, query.Locale))
                ],
                EmergencyKinds = ResolveKinds(),
                AuthorityTypes =
                    [.. Enum.GetValues<AuthorityType>().Select(type => type.ToString())],

                // Ascending by urgency, which is the enum's own order and the order a client should
                // render a picker in. Sorting alphabetically here would put Critical between
                // nothing and Medium, which is exactly the confusion the rank column exists to stop.
                Severities =
                    [.. Enum.GetValues<NoticeSeverity>()
                        .OrderBy(severity => (int)severity)
                        .Select(severity => severity.ToString())],
                SortOptions =
                    [.. Enum.GetValues<NoticeSortOrder>().Select(IntelligenceWireNames.ToWire)],
                SearchEngine = _engine.Name,
                SearchAvailable = await _engine.IsAvailableAsync(cancellationToken),
                AuthorityVerificationAvailable = _verification.HasProvider,
                SourceValidators = _sources.ValidatorNames,
                AnalyticsSinks = _analytics.SinkNames,
                HolidayHorizonDays = _options.Value.HolidayHorizonDays
            });
    }

    /// <summary>
    /// The emergency vocabulary, first contributor winning on a repeated key.
    /// <para>
    /// First rather than last, matching how the category seeder resolves collisions: a deployment
    /// that registers below the shipped contributor is deliberately overriding it, and a deployment
    /// that registers above is adding to it.
    /// </para>
    /// </summary>
    private IReadOnlyList<EmergencyContactKindResponse> ResolveKinds()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var resolved = new List<EmergencyContactKindResponse>();

        foreach (var seed in _kinds.SelectMany(contributor => contributor.GetKinds()))
        {
            var key = IntelligenceNormalization.Key(seed.Key);

            if (key is null || !seen.Add(key))
            {
                continue;
            }

            resolved.Add(new EmergencyContactKindResponse
            {
                Key = key,
                Name = seed.Name,
                Icon = seed.Icon,
                Order = seed.Order,
                IsEssential = seed.IsEssential
            });
        }

        return [.. resolved.OrderBy(kind => kind.Order).ThenBy(kind => kind.Key, StringComparer.Ordinal)];
    }
}

/// <summary>
/// How much one country's official information is being used.
/// <para>
/// The rates are computed here rather than left to a client, because two clients computing them would
/// eventually disagree about the denominator — and a rate whose denominator nobody agrees on is a
/// number an editorial team will make a decision on and be wrong.
/// </para>
/// </summary>
public sealed class GetIntelligenceAnalyticsQueryHandler
    : IQueryHandler<GetIntelligenceAnalyticsQuery, IntelligenceAnalyticsResponse>
{
    private readonly ICountryIntelligenceAnalyticsRepository _analytics;
    private readonly IOfficialNoticeRepository _notices;
    private readonly IntelligenceReferenceResolver _references;
    private readonly IClock _clock;

    public GetIntelligenceAnalyticsQueryHandler(
        ICountryIntelligenceAnalyticsRepository analytics,
        IOfficialNoticeRepository notices,
        IntelligenceReferenceResolver references,
        IClock clock)
    {
        _analytics = analytics;
        _notices = notices;
        _references = references;
        _clock = clock;
    }

    public async Task<Result<IntelligenceAnalyticsResponse>> Handle(
        GetIntelligenceAnalyticsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var country = IntelligenceNormalization.Country(query.CountryCode);

        if (country is null)
        {
            return Result.Failure<IntelligenceAnalyticsResponse>(
                IntelligenceErrors.CountryInvalid);
        }

        var counters = await _analytics.GetForCountryAsync(country, cancellationToken);
        var now = _clock.UtcNow;

        var mostViewed = await _notices.TakeAsync(
            new NoticeQueryFilter
            {
                CountryCode = country,
                Sort = NoticeSortOrder.MostViewed,
                AsOf = now
            },
            IntelligenceLimits.DashboardBandSize,
            cancellationToken);

        var references = await _references.ResolveAsync(mostViewed, cancellationToken);

        var views = counters?.CountOf(IntelligenceCounterKinds.NoticeViewed) ?? 0;
        var clicks = counters?.CountOf(IntelligenceCounterKinds.NoticeClickThrough) ?? 0;
        var completions = counters?.CountOf(IntelligenceCounterKinds.NoticeCompleted) ?? 0;

        return Result.Success(
            new IntelligenceAnalyticsResponse
            {
                CountryCode = country,
                NoticeViews = views,
                ClickThroughs = clicks,
                ReadCompletions = completions,
                DashboardViews = counters?.CountOf(IntelligenceCounterKinds.DashboardViewed) ?? 0,
                EmbassyViews = counters?.CountOf(IntelligenceCounterKinds.EmbassyViewed) ?? 0,
                EmergencyViews = counters?.CountOf(IntelligenceCounterKinds.EmergencyViewed) ?? 0,
                VisaViews = counters?.CountOf(IntelligenceCounterKinds.VisaViewed) ?? 0,
                HolidayViews = counters?.CountOf(IntelligenceCounterKinds.HolidaysViewed) ?? 0,
                GuideCrossNavigations =
                    counters?.CountOf(IntelligenceCounterKinds.GuideCrossNavigation) ?? 0,
                Searches = counters?.CountOf(IntelligenceCounterKinds.Searched) ?? 0,
                UnansweredSearches =
                    counters?.CountOf(IntelligenceCounterKinds.SearchedUnanswered) ?? 0,
                RecommendationsAccepted =
                    counters?.CountOf(IntelligenceCounterKinds.RecommendationAccepted) ?? 0,
                NotificationsFollowed =
                    counters?.CountOf(IntelligenceCounterKinds.NotificationFollowed) ?? 0,
                ClickThroughRate = Rate(clicks, views),
                ReadCompletionRate = Rate(completions, views),
                LastActivityAt = counters?.LastActivityAt,
                MostViewed =
                [
                    .. mostViewed.Select(notice => OfficialNoticeMapper.ToCard(
                        notice,
                        references.Category(notice.CategoryKey),
                        references.Authority(notice.AuthorityId),
                        now,
                        locale: null))
                ]
            });
    }

    /// <summary>
    /// A percentage, or null when the denominator is zero.
    /// <para>
    /// Null rather than zero, because "nobody clicked through on zero views" and "nobody clicked
    /// through on ten thousand views" are opposite findings, and a dashboard rendering 0% for both
    /// tells an editorial team the wrong one.
    /// </para>
    /// </summary>
    private static double? Rate(long numerator, long denominator) =>
        denominator == 0 ? null : Math.Round(numerator * 100d / denominator, 2);
}
