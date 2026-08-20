namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// Everything a country's official information layer shows, in one response.
/// <para>
/// <b>The ten bands the brief names, and it is one response rather than ten calls on purpose.</b> A
/// country page opens on a phone, frequently on a connection that is the reason somebody is reading
/// about their country in the first place, and ten round trips to render one screen is ten chances to
/// show a spinner where an emergency number should be.
/// </para>
/// <para>
/// <b>The bands are deliberately short.</b> Each carries a handful of rows and every one of them has
/// a corresponding paged endpoint for somebody who wants the rest — the same rule
/// <c>CountryDashboardResponse</c> states, for the same reason.
/// </para>
/// <para>
/// <b>This is additive to the country dashboard, never a replacement for it.</b> The brief is
/// explicit that country dashboards must not be duplicated: <c>CountryDashboardResponse</c> gains one
/// property carrying a summary of this, and a client that wants the whole intelligence layer asks for
/// it here. Two dashboards would mean two answers to "what does this country page show".
/// </para>
/// </summary>
public sealed record CountryIntelligenceResponse
{
    /// <summary>Upper-case ISO 3166-1 alpha-2.</summary>
    public required string CountryCode { get; init; }

    /// <summary>The locale actually served, after the fallback.</summary>
    public required string ResolvedLocale { get; init; }

    /// <summary>The headline numbers.</summary>
    public required CountryIntelligenceStatisticsResponse Statistics { get; init; }

    // ── The bands ───────────────────────────────────────────────────────────────────────────

    /// <summary>The most recent official notices, in briefing order.</summary>
    public IReadOnlyList<OfficialNoticeCardResponse> LatestNotices { get; init; } = [];

    /// <summary>
    /// Anything critical or high that is live right now.
    /// <para>
    /// Separate from the latest band rather than filtered out of it: a member scanning a country page
    /// during a cyclone should not have to notice that the third card is red.
    /// </para>
    /// </summary>
    public IReadOnlyList<OfficialNoticeCardResponse> SafetyAlerts { get; init; } = [];

    /// <summary>Live notices from the visa and immigration categories.</summary>
    public IReadOnlyList<OfficialNoticeCardResponse> VisaUpdates { get; init; } = [];

    /// <summary>Live notices from the government announcement categories.</summary>
    public IReadOnlyList<OfficialNoticeCardResponse> GovernmentAdvisories { get; init; } = [];

    /// <summary>Missions in this country and missions serving its nationals.</summary>
    public IReadOnlyList<EmbassyResponse> Embassies { get; init; } = [];

    /// <summary>
    /// The emergency numbers, in full rather than as a count.
    /// <para>
    /// The one band that carries its whole content on the dashboard. Everything else here is a
    /// summary with a "see all" behind it; making somebody tap through to an ambulance number would
    /// be indefensible.
    /// </para>
    /// </summary>
    public EmergencyContactsResponse? Emergency { get; init; }

    /// <summary>Published advisories that apply right now.</summary>
    public IReadOnlyList<VisaAdvisoryResponse> ConsularServices { get; init; } = [];

    /// <summary>What falls inside the dashboard's horizon, soonest first.</summary>
    public IReadOnlyList<PublicHolidayResponse> UpcomingHolidays { get; init; } = [];

    /// <summary>The bodies that issue for this country, verified first.</summary>
    public IReadOnlyList<IssuingAuthorityResponse> OfficialContacts { get; init; } = [];

    /// <summary>What is written, per notice category. The filter chips with their badges.</summary>
    public IReadOnlyList<NoticeCategoryCountResponse> CategoryBreakdown { get; init; } = [];
}

/// <summary>The headline numbers on a country's intelligence layer.</summary>
public sealed record CountryIntelligenceStatisticsResponse
{
    /// <summary>Notices that are guidance right now.</summary>
    public required int ActiveNoticeCount { get; init; }

    /// <summary>Live notices at critical or high severity. The number that decides a banner.</summary>
    public required int UrgentNoticeCount { get; init; }

    public required int EmbassyCount { get; init; }

    public required int VisaAdvisoryCount { get; init; }

    public required int EmergencyContactCount { get; init; }

    /// <summary>Holidays inside the dashboard's horizon.</summary>
    public required int UpcomingHolidayCount { get; init; }

    public required int VerifiedAuthorityCount { get; init; }

    /// <summary>
    /// When anything official about this country last changed.
    /// <para>
    /// The freshness signal an editorial team reads and a member trusts. Null means nothing has been
    /// published for this country yet, which a client renders as an empty state rather than as zero.
    /// </para>
    /// </summary>
    public DateTimeOffset? LastPublishedAt { get; init; }
}

/// <summary>How many current notices sit under one category.</summary>
public sealed record NoticeCategoryCountResponse
{
    public required string Key { get; init; }

    /// <summary>Already localized.</summary>
    public required string Name { get; init; }

    public string? Icon { get; init; }

    public required int Count { get; init; }
}
