using Diaspora.Modules.Guides.Application.Intelligence.Mapping;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Embassies;
using Diaspora.Modules.Guides.Domain.Emergency;
using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Holidays;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Domain.Policies;
using Diaspora.Modules.Guides.Domain.Visas;
using Diaspora.Modules.Guides.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Guides.Application.Intelligence.Services;

/// <summary>
/// Assembles a country's whole intelligence layer.
/// <para>
/// <b>The single class that composes it, so there is one answer to what a country page shows.</b>
/// B8.1's <c>KnowledgeDirectoryComposer</c> makes the same argument and it holds here with more
/// force: an emergency band assembled differently on two screens is two answers to "who do I call".
/// </para>
/// <para>
/// <b>What it costs, stated plainly:</b> four notice reads (latest, urgent, visa, government), one
/// embassy read, one emergency read, one advisory read, one holiday read, one category-count group,
/// one authority listing, and <em>one</em> batched reference resolution over the union of every
/// notice the page produced. Twelve queries for a whole country page, bounded by the page rather than
/// by the archive behind it — a country with ten notices and a country with ten thousand cost the
/// same.
/// </para>
/// <para>
/// <b>The four notice bands are read separately rather than filtered from one.</b> Reading a hundred
/// notices and partitioning them in memory would be one query, and would silently produce an empty
/// visa band for a country whose hundred most recent notices happen to be about something else. Each
/// band asks for what it is.
/// </para>
/// </summary>
public sealed class CountryIntelligenceComposer
{
    /// <summary>
    /// The categories the visa band draws from.
    /// <para>
    /// Keys rather than a flag on the category, because "what the visa band shows" is a presentation
    /// decision and the alert family is a routing one — B8.1's navigation-is-not-filing distinction
    /// applied to a different pair. A deployment that renames these categories changes this list;
    /// one that adds a category to the band does the same. Neither is a change to what a notice is.
    /// </para>
    /// </summary>
    private static readonly string[] VisaCategoryKeys =
        ["visa-advisories", "immigration-updates", "border-changes", "travel-restrictions"];

    private readonly IOfficialNoticeRepository _notices;
    private readonly INoticeCategoryRepository _categories;
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IEmbassyRepository _embassies;
    private readonly ICountryEmergencyDirectoryRepository _emergency;
    private readonly IVisaAdvisoryRepository _advisories;
    private readonly IPublicHolidayRepository _holidays;
    private readonly IntelligenceReferenceResolver _references;
    private readonly IOptions<IntelligenceOptions> _options;
    private readonly IClock _clock;

    public CountryIntelligenceComposer(
        IOfficialNoticeRepository notices,
        INoticeCategoryRepository categories,
        IIssuingAuthorityRepository authorities,
        IEmbassyRepository embassies,
        ICountryEmergencyDirectoryRepository emergency,
        IVisaAdvisoryRepository advisories,
        IPublicHolidayRepository holidays,
        IntelligenceReferenceResolver references,
        IOptions<IntelligenceOptions> options,
        IClock clock)
    {
        _notices = notices;
        _categories = categories;
        _authorities = authorities;
        _embassies = embassies;
        _emergency = emergency;
        _advisories = advisories;
        _holidays = holidays;
        _references = references;
        _options = options;
        _clock = clock;
    }

    /// <summary>The whole intelligence layer for one country.</summary>
    public async Task<CountryIntelligenceResponse> ComposeAsync(
        string countryCode, string? locale, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var band = IntelligenceLimits.DashboardBandSize;

        var latest = await _notices.TakeAsync(
            Band(countryCode, now), band, cancellationToken);

        var urgent = await _notices.TakeAsync(
            Band(countryCode, now) with { MinimumSeverity = NoticeSeverity.High },
            band,
            cancellationToken);

        var visa = await _notices.TakeAsync(
            Band(countryCode, now) with { CategoryKeys = VisaCategoryKeys },
            band,
            cancellationToken);

        var government = await _notices.TakeAsync(
            Band(countryCode, now) with
            {
                CategoryKeys = ["government-announcements", "election-information", "legal-changes"]
            },
            band,
            cancellationToken);

        // One batched resolution over every notice this page produced, not one per band. The bands
        // overlap heavily — an urgent visa notice appears in three of them — so the union is
        // typically half the sum.
        var references = await _references.ResolveAsync(
            [.. latest.Concat(urgent).Concat(visa).Concat(government)], cancellationToken);

        var embassies = await _embassies.TakeAsync(
            countryCode, serving: null, band, cancellationToken);

        var emergency = await _emergency.GetForCountryAsync(countryCode, cancellationToken);

        var advisories = await _advisories.TakeAsync(
            countryCode, appliesTo: null, now, band, cancellationToken);

        var holidays = await _holidays.ListBetweenAsync(
            countryCode,
            now,
            now.AddDays(_options.Value.HolidayHorizonDays),
            cancellationToken);

        var authorities = await _authorities.ListForCountryAsync(
            countryCode, type: null, verifiedOnly: false, cancellationToken);

        var breakdown = await _notices.CountByCategoryAsync(countryCode, now, cancellationToken);

        var catalogue = await _categories.GetByKeysAsync([.. breakdown.Keys], cancellationToken);

        return new CountryIntelligenceResponse
        {
            CountryCode = countryCode,
            ResolvedLocale = IntelligenceNormalization.Locale(locale) ?? string.Empty,
            Statistics = await BuildStatisticsAsync(
                countryCode,
                now,
                embassies.Count,
                emergency,
                holidays,
                authorities,
                cancellationToken),

            LatestNotices = Cards(latest, references, now, locale),
            SafetyAlerts = Cards(urgent, references, now, locale),
            VisaUpdates = Cards(visa, references, now, locale),
            GovernmentAdvisories = Cards(government, references, now, locale),

            Embassies =
                [.. embassies.Select(embassy =>
                    IntelligenceDirectoryMapper.ToResponse(embassy, locale))],

            Emergency = emergency is null
                ? null
                : IntelligenceDirectoryMapper.ToResponse(emergency, locale),

            ConsularServices =
            [
                .. advisories.Select(advisory => IntelligenceDirectoryMapper.ToResponse(
                    advisory,
                    references.Authority(advisory.AuthorityId),
                    now,
                    locale))
            ],

            UpcomingHolidays =
                [.. holidays.Select(holiday =>
                    IntelligenceDirectoryMapper.ToResponse(holiday, locale))],

            OfficialContacts =
            [
                .. authorities
                    .OrderByDescending(authority => authority.IsVerified)
                    .ThenBy(authority => authority.Name, StringComparer.Ordinal)
                    .Take(band)
                    .Select(authority => IntelligenceCatalogueMapper.ToResponse(authority))
            ],

            CategoryBreakdown =
            [
                .. breakdown
                    .Select(entry =>
                    {
                        var category = catalogue.GetValueOrDefault(entry.Key);
                        var (name, _) = category?.Localized(locale) ?? (entry.Key, null);

                        return new NoticeCategoryCountResponse
                        {
                            Key = entry.Key,
                            Name = name,
                            Icon = category?.Icon,
                            Count = entry.Value
                        };
                    })
                    .OrderByDescending(row => row.Count)
                    .ThenBy(row => row.Key, StringComparer.Ordinal)
            ]
        };
    }

    /// <summary>
    /// The band that goes on the country <em>knowledge</em> dashboard — three notices, the emergency
    /// numbers, and the counts.
    /// <para>
    /// <b>This is the whole of B8.2's extension to B8.1's dashboard, and it is deliberately small.</b>
    /// The knowledge dashboard is already seven guide shelves, a breakdown, a trending list and a set
    /// of context bands; a client that wants the full intelligence layer follows the route on this
    /// summary. Composing the whole thing into a response that already carries that much would
    /// produce something nobody renders.
    /// </para>
    /// </summary>
    public async Task<CountryIntelligenceSummaryResponse> ComposeSummaryAsync(
        string countryCode, string? locale, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var notices = await _notices.TakeAsync(Band(countryCode, now), 3, cancellationToken);
        var references = await _references.ResolveAsync(notices, cancellationToken);

        var emergency = await _emergency.GetForCountryAsync(countryCode, cancellationToken);

        var holidays = await _holidays.ListBetweenAsync(
            countryCode, now, now.AddDays(_options.Value.HolidayHorizonDays), cancellationToken);

        var active = await _notices.CountAsync(Band(countryCode, now), cancellationToken);

        var urgent = await _notices.CountAsync(
            Band(countryCode, now) with { MinimumSeverity = NoticeSeverity.High },
            cancellationToken);

        var embassyCount = await _embassies.CountAsync(
            countryCode, serving: null, cancellationToken);

        return new CountryIntelligenceSummaryResponse
        {
            ActiveNoticeCount = active,
            UrgentNoticeCount = urgent,
            Notices = Cards(notices, references, now, locale),
            EmergencyContacts = emergency is null
                ? []
                : IntelligenceDirectoryMapper.ToResponse(emergency, locale).Contacts,
            EmbassyCount = embassyCount,
            UpcomingHolidays =
                [.. holidays
                    .Take(IntelligenceLimits.DashboardBandSize)
                    .Select(holiday => IntelligenceDirectoryMapper.ToResponse(holiday, locale))],
            LastPublishedAt = await LastPublishedAtAsync(countryCode, now, cancellationToken),
            Route = $"{GuidesModuleMetadata.IntelligenceRoutePrefix}/countries/{countryCode}"
        };
    }

    /// <summary>
    /// The filter every reader-facing band shares: this country, live right now, in briefing order.
    /// <para>
    /// One expression rather than one per band, because a band that quietly differed — including
    /// expired notices, say — would produce a dashboard where one row contradicts another.
    /// </para>
    /// </summary>
    private static NoticeQueryFilter Band(string countryCode, DateTimeOffset now) =>
        new()
        {
            CountryCode = countryCode,
            ActiveOnly = true,
            AsOf = now,
            Sort = NoticeSortOrder.Priority
        };

    private static IReadOnlyList<OfficialNoticeCardResponse> Cards(
        IReadOnlyList<OfficialNotice> notices,
        IntelligenceReferences references,
        DateTimeOffset now,
        string? locale) =>
    [
        .. notices.Select(notice => OfficialNoticeMapper.ToCard(
            notice,
            references.Category(notice.CategoryKey),
            references.Authority(notice.AuthorityId),
            now,
            locale))
    ];

    private async Task<CountryIntelligenceStatisticsResponse> BuildStatisticsAsync(
        string countryCode,
        DateTimeOffset now,
        int embassyCount,
        CountryEmergencyDirectory? emergency,
        IReadOnlyList<PublicHoliday> holidays,
        IReadOnlyList<IssuingAuthority> authorities,
        CancellationToken cancellationToken)
    {
        var active = await _notices.CountAsync(Band(countryCode, now), cancellationToken);

        var urgent = await _notices.CountAsync(
            Band(countryCode, now) with { MinimumSeverity = NoticeSeverity.High },
            cancellationToken);

        var advisoryCount = await _advisories.CountAsync(countryCode, now, cancellationToken);

        return new CountryIntelligenceStatisticsResponse
        {
            ActiveNoticeCount = active,
            UrgentNoticeCount = urgent,
            EmbassyCount = embassyCount,
            VisaAdvisoryCount = advisoryCount,
            EmergencyContactCount = emergency?.Contacts.Count ?? 0,
            UpcomingHolidayCount = holidays.Count,
            VerifiedAuthorityCount = authorities.Count(authority => authority.IsVerified),
            LastPublishedAt = await LastPublishedAtAsync(countryCode, now, cancellationToken)
        };
    }

    /// <summary>
    /// When anything official about this country last changed.
    /// <para>
    /// <b>Its own single-row query rather than a maximum over the band, and the difference is
    /// correctness rather than cost.</b> The bands are ordered by severity first, so the maximum
    /// publication instant across five of them is the most recent of the five most <em>serious</em>
    /// notices — which for a country with a standing critical advisory is a date from six months ago,
    /// rendered under a heading that says "last updated". A freshness signal a member reads and
    /// trusts has to be the real one, and an indexed top-one on publication time costs almost
    /// nothing.
    /// </para>
    /// </summary>
    private async Task<DateTimeOffset?> LastPublishedAtAsync(
        string countryCode, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var newest = await _notices.TakeAsync(
            Band(countryCode, now) with { Sort = NoticeSortOrder.Newest },
            1,
            cancellationToken);

        return newest.Count == 0 ? null : newest[0].PublishedAt;
    }
}
