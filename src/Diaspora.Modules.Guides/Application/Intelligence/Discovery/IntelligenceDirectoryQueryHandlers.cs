using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Application.Intelligence.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Embassies;
using Diaspora.Modules.Guides.Domain.Emergency;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Holidays;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Visas;
using Diaspora.Modules.Guides.Extensibility.Analytics;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Application.Intelligence.Discovery;

/// <summary>The notice category catalogue, ordered.</summary>
public sealed class ListNoticeCategoriesQueryHandler
    : IQueryHandler<ListNoticeCategoriesQuery, IReadOnlyList<NoticeCategoryResponse>>
{
    private readonly INoticeCategoryRepository _categories;
    private readonly IntelligenceAuthorizationService _authorization;

    public ListNoticeCategoriesQueryHandler(
        INoticeCategoryRepository categories,
        IntelligenceAuthorizationService authorization)
    {
        _categories = categories;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<NoticeCategoryResponse>>> Handle(
        ListNoticeCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var isEditor = _authorization.IsEditor;

        var categories = await _categories.ListAsync(
            isEditor && query.IncludeArchived, cancellationToken);

        // An editor gets the default labels and the revisions; everybody else gets the localized
        // reader view. See the mapper for why an editor must not be shown a translation of the
        // string they are about to overwrite.
        return Result.Success<IReadOnlyList<NoticeCategoryResponse>>(
            isEditor
                ? [.. categories.Select(IntelligenceCatalogueMapper.ToEditorResponse)]
                : [.. categories.Select(category =>
                    IntelligenceCatalogueMapper.ToResponse(category, query.Locale))]);
    }
}

/// <summary>The bodies that issue for one country.</summary>
public sealed class ListIssuingAuthoritiesQueryHandler
    : IQueryHandler<ListIssuingAuthoritiesQuery, IReadOnlyList<IssuingAuthorityResponse>>
{
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IntelligenceAuthorizationService _authorization;

    public ListIssuingAuthoritiesQueryHandler(
        IIssuingAuthorityRepository authorities,
        IntelligenceAuthorizationService authorization)
    {
        _authorities = authorities;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<IssuingAuthorityResponse>>> Handle(
        ListIssuingAuthoritiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var authorities = await _authorities.ListForCountryAsync(
            query.CountryCode,
            IntelligenceWireNames.ParseAuthorityType(query.AuthorityType),
            query.VerifiedOnly,
            cancellationToken);

        var forEditor = _authorization.IsEditor;

        return Result.Success<IReadOnlyList<IssuingAuthorityResponse>>(
            [.. authorities.Select(authority =>
                IntelligenceCatalogueMapper.ToResponse(authority, forEditor))]);
    }
}

/// <summary>One issuing authority.</summary>
public sealed class GetIssuingAuthorityQueryHandler
    : IQueryHandler<GetIssuingAuthorityQuery, IssuingAuthorityResponse>
{
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IntelligenceAuthorizationService _authorization;

    public GetIssuingAuthorityQueryHandler(
        IIssuingAuthorityRepository authorities,
        IntelligenceAuthorizationService authorization)
    {
        _authorities = authorities;
        _authorization = authorization;
    }

    public async Task<Result<IssuingAuthorityResponse>> Handle(
        GetIssuingAuthorityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var authority = await _authorities.GetAsync(
            IssuingAuthorityId.From(query.IssuingAuthorityId), cancellationToken);

        return authority is null
            ? Result.Failure<IssuingAuthorityResponse>(IntelligenceErrors.AuthorityNotFound)
            : Result.Success(
                IntelligenceCatalogueMapper.ToResponse(authority, _authorization.IsEditor));
    }
}

/// <summary>One page of the embassy directory.</summary>
public sealed class ListEmbassiesQueryHandler
    : IQueryHandler<ListEmbassiesQuery, CursorPagedResult<EmbassyResponse>>
{
    private readonly IEmbassyRepository _embassies;

    public ListEmbassiesQueryHandler(IEmbassyRepository embassies) => _embassies = embassies;

    public async Task<Result<CursorPagedResult<EmbassyResponse>>> Handle(
        ListEmbassiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = await _embassies.ListAsync(
            query.LocatedIn,
            query.Serving,
            query.VerifiedOnly,
            query.Text,
            query.Cursor,
            query.Limit ?? IntelligenceLimits.DefaultPageSize,
            cancellationToken);

        return Result.Success(
            CursorPagedResult<EmbassyResponse>.Create(
                [.. page.Items.Select(embassy =>
                    IntelligenceDirectoryMapper.ToResponse(embassy, query.Locale))],
                page.NextCursor,
                page.HasMore));
    }
}

/// <summary>One mission in full.</summary>
public sealed class GetEmbassyQueryHandler : IQueryHandler<GetEmbassyQuery, EmbassyResponse>
{
    private readonly IEmbassyRepository _embassies;
    private readonly IntelligenceAnalyticsRecorder _analytics;
    private readonly IntelligenceAuthorizationService _authorization;

    public GetEmbassyQueryHandler(
        IEmbassyRepository embassies,
        IntelligenceAnalyticsRecorder analytics,
        IntelligenceAuthorizationService authorization)
    {
        _embassies = embassies;
        _analytics = analytics;
        _authorization = authorization;
    }

    public async Task<Result<EmbassyResponse>> Handle(
        GetEmbassyQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var embassy = await _embassies.GetAsync(
            EmbassyId.From(query.EmbassyId), cancellationToken);

        if (embassy is null)
        {
            return Result.Failure<EmbassyResponse>(IntelligenceErrors.EmbassyNotFound);
        }

        // A directory entry has no editorial workflow to hide, so unlike a notice this counts the
        // open on the read. There is nothing here a prefetch could inflate that matters: the number
        // answers "is this mission's page being used", and an approximate answer is the contract.
        await _analytics.RecordAsync(
            _analytics.Signal(
                IntelligenceAnalyticsSignalKinds.EmbassyViewed,
                embassy.CountryCode,
                embassyId: embassy.Id.Value,
                locale: query.Locale),
            cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(
                embassy, query.Locale, _authorization.IsEditor));
    }
}

/// <summary>One country's emergency information.</summary>
public sealed class GetEmergencyContactsQueryHandler
    : IQueryHandler<GetEmergencyContactsQuery, EmergencyContactsResponse>
{
    private readonly ICountryEmergencyDirectoryRepository _directories;
    private readonly IntelligenceAnalyticsRecorder _analytics;
    private readonly IntelligenceAuthorizationService _authorization;

    public GetEmergencyContactsQueryHandler(
        ICountryEmergencyDirectoryRepository directories,
        IntelligenceAnalyticsRecorder analytics,
        IntelligenceAuthorizationService authorization)
    {
        _directories = directories;
        _analytics = analytics;
        _authorization = authorization;
    }

    public async Task<Result<EmergencyContactsResponse>> Handle(
        GetEmergencyContactsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var directory = await _directories.GetForCountryAsync(
            query.CountryCode, cancellationToken);

        if (directory is null)
        {
            return Result.Failure<EmergencyContactsResponse>(
                IntelligenceErrors.EmergencyDirectoryNotFound);
        }

        if (query.RecordView)
        {
            await _analytics.RecordAsync(
                _analytics.Signal(
                    IntelligenceAnalyticsSignalKinds.EmergencyViewed,
                    directory.CountryCode,
                    locale: query.Locale),
                cancellationToken);
        }

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(
                directory, query.Locale, _authorization.IsEditor));
    }
}

/// <summary>One page of visa advisories for a destination.</summary>
public sealed class ListVisaAdvisoriesQueryHandler
    : IQueryHandler<ListVisaAdvisoriesQuery, CursorPagedResult<VisaAdvisoryResponse>>
{
    private readonly IVisaAdvisoryRepository _advisories;
    private readonly IntelligenceReferenceResolver _references;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IClock _clock;

    public ListVisaAdvisoriesQueryHandler(
        IVisaAdvisoryRepository advisories,
        IntelligenceReferenceResolver references,
        IntelligenceAuthorizationService authorization,
        IClock clock)
    {
        _advisories = advisories;
        _references = references;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<CursorPagedResult<VisaAdvisoryResponse>>> Handle(
        ListVisaAdvisoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _clock.UtcNow;
        var isEditor = _authorization.IsEditor;

        var page = await _advisories.ListAsync(
            query.CountryCode,
            query.AppliesTo,
            publishedOnly: !isEditor,
            activeOnly: query.ActiveOnly && !isEditor,
            now,
            query.Cursor,
            query.Limit ?? IntelligenceLimits.DefaultPageSize,
            cancellationToken);

        var authorities = await _references.ResolveAuthoritiesAsync(
            [.. page.Items.Select(advisory => advisory.AuthorityId)], cancellationToken);

        return Result.Success(
            CursorPagedResult<VisaAdvisoryResponse>.Create(
                [
                    .. page.Items.Select(advisory => IntelligenceDirectoryMapper.ToResponse(
                        advisory,
                        authorities.GetValueOrDefault(advisory.AuthorityId),
                        now,
                        query.Locale,
                        isEditor))
                ],
                page.NextCursor,
                page.HasMore));
    }
}

/// <summary>One visa advisory in full.</summary>
public sealed class GetVisaAdvisoryQueryHandler
    : IQueryHandler<GetVisaAdvisoryQuery, VisaAdvisoryResponse>
{
    private readonly IVisaAdvisoryRepository _advisories;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IntelligenceAnalyticsRecorder _analytics;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IClock _clock;

    public GetVisaAdvisoryQueryHandler(
        IVisaAdvisoryRepository advisories,
        IIssuingAuthorityRepository authorities,
        IntelligenceAnalyticsRecorder analytics,
        IntelligenceAuthorizationService authorization,
        IClock clock)
    {
        _advisories = advisories;
        _authorities = authorities;
        _analytics = analytics;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<VisaAdvisoryResponse>> Handle(
        GetVisaAdvisoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var advisory = await _advisories.GetAsync(
            VisaAdvisoryId.From(query.VisaAdvisoryId), cancellationToken);

        if (advisory is null)
        {
            return Result.Failure<VisaAdvisoryResponse>(
                IntelligenceErrors.VisaAdvisoryNotFound);
        }

        var isEditor = _authorization.IsEditor;

        if (!isEditor && advisory.Status != Domain.Enums.VisaAdvisoryStatus.Published)
        {
            return Result.Failure<VisaAdvisoryResponse>(
                IntelligenceErrors.VisaAdvisoryNotFound);
        }

        var authority = await _authorities.GetIncludingArchivedAsync(
            advisory.AuthorityId, cancellationToken);

        await _analytics.RecordAsync(
            _analytics.Signal(
                IntelligenceAnalyticsSignalKinds.VisaViewed,
                advisory.CountryCode,
                visaAdvisoryId: advisory.Id.Value,
                locale: query.Locale),
            cancellationToken);

        return Result.Success(
            IntelligenceDirectoryMapper.ToResponse(
                advisory, authority, _clock.UtcNow, query.Locale, isEditor));
    }
}

/// <summary>
/// One country's public holiday calendar.
/// <para>
/// A range wins over a year when both are given, because a caller that supplied a range meant it —
/// the year is what a calendar screen sends by default and the range is what a caller sends
/// deliberately.
/// </para>
/// </summary>
public sealed class ListPublicHolidaysQueryHandler
    : IQueryHandler<ListPublicHolidaysQuery, IReadOnlyList<PublicHolidayResponse>>
{
    private readonly IPublicHolidayRepository _holidays;
    private readonly IntelligenceAnalyticsRecorder _analytics;
    private readonly IntelligenceAuthorizationService _authorization;
    private readonly IClock _clock;

    public ListPublicHolidaysQueryHandler(
        IPublicHolidayRepository holidays,
        IntelligenceAnalyticsRecorder analytics,
        IntelligenceAuthorizationService authorization,
        IClock clock)
    {
        _holidays = holidays;
        _analytics = analytics;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<IReadOnlyList<PublicHolidayResponse>>> Handle(
        ListPublicHolidaysQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var holidays = query is { From: { } from, Until: { } until }
            ? await _holidays.ListBetweenAsync(
                query.CountryCode, from, until, cancellationToken)
            : await _holidays.ListForYearAsync(
                query.CountryCode, query.Year ?? _clock.UtcNow.Year, cancellationToken);

        await _analytics.RecordAsync(
            _analytics.Signal(
                IntelligenceAnalyticsSignalKinds.HolidaysViewed,
                IntelligenceNormalization.Country(query.CountryCode),
                locale: query.Locale),
            cancellationToken);

        var forEditor = _authorization.IsEditor;

        return Result.Success<IReadOnlyList<PublicHolidayResponse>>(
            [.. holidays.Select(holiday =>
                IntelligenceDirectoryMapper.ToResponse(holiday, query.Locale, forEditor))]);
    }
}
