namespace Diaspora.Modules.Guides.Contracts.V1.Responses;

/// <summary>
/// The intelligence layer as it appears <em>on the country knowledge dashboard</em> — a band, not a
/// page.
/// <para>
/// <b>This is how B8.2 extends the country dashboard without duplicating it.</b> The brief is
/// explicit on both halves: every country dashboard now surfaces official information, and country
/// dashboards must not be duplicated. So <c>CountryDashboardResponse</c> gains exactly one property
/// carrying this, and a client that wants the full intelligence layer follows the route on it.
/// </para>
/// <para>
/// <b>Three notices and the emergency numbers, and nothing else.</b> The dashboard is already
/// composed of seven guide shelves, a breakdown, a trending list and a set of context bands; adding
/// ten more bands to it would produce a response nobody renders. What is here is what a member
/// glancing at a country page has to see: is anything urgent, and who do I call.
/// </para>
/// </summary>
public sealed record CountryIntelligenceSummaryResponse
{
    /// <summary>Notices that are guidance right now.</summary>
    public required int ActiveNoticeCount { get; init; }

    /// <summary>Live notices at critical or high severity. The number that decides a banner.</summary>
    public required int UrgentNoticeCount { get; init; }

    /// <summary>The top of the briefing — severity first, then the editor's pin, then recency.</summary>
    public IReadOnlyList<OfficialNoticeCardResponse> Notices { get; init; } = [];

    /// <summary>
    /// The emergency numbers, in full.
    /// <para>
    /// Carried on the dashboard band rather than behind the route, because a member who has opened
    /// their country page during an emergency has already spent the only tap they were going to
    /// spend.
    /// </para>
    /// </summary>
    public IReadOnlyList<EmergencyContactResponse> EmergencyContacts { get; init; } = [];

    public required int EmbassyCount { get; init; }

    /// <summary>What falls inside the dashboard's horizon. Names and dates only.</summary>
    public IReadOnlyList<PublicHolidayResponse> UpcomingHolidays { get; init; } = [];

    /// <summary>When anything official about this country last changed.</summary>
    public DateTimeOffset? LastPublishedAt { get; init; }

    /// <summary>Where to go for the whole intelligence layer. A relative API path.</summary>
    public required string Route { get; init; }
}
