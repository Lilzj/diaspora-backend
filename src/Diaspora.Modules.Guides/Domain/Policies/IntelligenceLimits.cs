namespace Diaspora.Modules.Guides.Domain.Policies;

/// <summary>
/// The ceilings the Country Intelligence aggregates enforce.
/// <para>
/// Constants rather than options, for the reason <see cref="GuideLimits"/> and
/// <see cref="KnowledgeLimits"/> both give: a bound that can be raised per deployment is a bound the
/// domain cannot rely on, and every invariant that depends on it becomes conditional.
/// </para>
/// <para>
/// <b>These sit between B8's and B8.1's, and the position says what a notice is.</b> A guide may run
/// to a hundred sections because somebody wrote a book; a navigation row may hold ten sources
/// because somebody arranged a menu. A notice is a document an authority published, so its body is
/// generous — a circular really can be four thousand words — while everything around it is tight,
/// because an official record with forty attachments and thirty related links has stopped being a
/// notice and become a filing cabinet.
/// </para>
/// </summary>
public static class IntelligenceLimits
{
    // ── Notices ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Characters in a notice title.</summary>
    public const int MaxNoticeTitle = 250;

    /// <summary>
    /// Characters in a notice summary.
    /// <para>
    /// Longer than a guide's abstract, because this is the text a push notification and a country
    /// dashboard card both render, and an official instruction truncated mid-clause is worse than no
    /// card at all.
    /// </para>
    /// </summary>
    public const int MaxNoticeSummary = 600;

    /// <summary>Characters in the full body of a notice.</summary>
    public const int MaxNoticeContent = 40_000;

    /// <summary>Characters in an official reference number — a circular or gazette number.</summary>
    public const int MaxReferenceNumber = 120;

    /// <summary>Characters in a URL. Long enough for the query strings government portals emit.</summary>
    public const int MaxUrl = 500;

    /// <summary>Languages one notice may declare it was officially issued in.</summary>
    public const int MaxNoticeLanguages = 12;

    /// <summary>Media attachments one notice may carry — the circular, the form, the map.</summary>
    public const int MaxNoticeAttachments = 20;

    /// <summary>
    /// Things one notice may point at, across every kind.
    /// <para>
    /// One budget for guides, paths, events, providers, jobs, homes, listings and embassies
    /// together, rather than a limit per kind. A notice with twelve links of one kind and none of
    /// another is a perfectly good notice; one with twelve of each is a link farm.
    /// </para>
    /// </summary>
    public const int MaxNoticeReferences = 24;

    /// <summary>Characters in the label a curator writes beside a reference.</summary>
    public const int MaxReferenceLabel = 200;

    /// <summary>Translations one notice may carry.</summary>
    public const int MaxNoticeLocalizations = 24;

    /// <summary>Characters in a note explaining why a notice was archived or rejected.</summary>
    public const int MaxNote = 500;

    // ── Categories ──────────────────────────────────────────────────────────────────────────

    /// <summary>Characters in a notice category key.</summary>
    public const int MaxCategoryKey = 64;

    /// <summary>Characters in a notice category name.</summary>
    public const int MaxCategoryName = 120;

    /// <summary>Characters in a notice category description.</summary>
    public const int MaxCategoryDescription = 1_000;

    // ── Authorities ─────────────────────────────────────────────────────────────────────────

    /// <summary>Characters in an authority's name.</summary>
    public const int MaxAuthorityName = 200;

    /// <summary>Characters in an authority's short name — the acronym staff and members use.</summary>
    public const int MaxAuthorityShortName = 60;

    /// <summary>Characters in an authority's description.</summary>
    public const int MaxAuthorityDescription = 2_000;

    // ── Embassies ───────────────────────────────────────────────────────────────────────────

    /// <summary>Characters in an embassy's display name.</summary>
    public const int MaxEmbassyName = 200;

    /// <summary>Characters in a postal address, which is multi-line and often long.</summary>
    public const int MaxAddress = 500;

    /// <summary>Characters in a telephone number, including extension and formatting.</summary>
    public const int MaxPhone = 40;

    /// <summary>Characters in an email address.</summary>
    public const int MaxEmail = 254;

    /// <summary>Characters in a free-text office-hours line for one day.</summary>
    public const int MaxOfficeHours = 200;

    /// <summary>Service lines one mission may list — passports, visas, notarial, legalisation.</summary>
    public const int MaxEmbassyServices = 30;

    /// <summary>Characters in the name of a consular service.</summary>
    public const int MaxServiceName = 150;

    /// <summary>Characters in a service's description.</summary>
    public const int MaxServiceDescription = 1_000;

    // ── Emergency information ───────────────────────────────────────────────────────────────

    /// <summary>Contacts one country's emergency directory may hold.</summary>
    public const int MaxEmergencyContacts = 40;

    /// <summary>Characters in an emergency contact kind key — <c>police</c>, <c>mental-health</c>.</summary>
    public const int MaxEmergencyKind = 64;

    /// <summary>Characters in an emergency contact's label, which is written per country.</summary>
    public const int MaxEmergencyLabel = 150;

    /// <summary>Characters in the note beside an emergency number — hours, languages, coverage.</summary>
    public const int MaxEmergencyNote = 500;

    // ── Visa advisories ─────────────────────────────────────────────────────────────────────

    /// <summary>Characters in a visa advisory title.</summary>
    public const int MaxVisaTitle = 250;

    /// <summary>Characters in a visa advisory summary.</summary>
    public const int MaxVisaSummary = 600;

    /// <summary>Characters in a visa advisory body.</summary>
    public const int MaxVisaContent = 20_000;

    /// <summary>Characters in a visa category name — <c>Student</c>, <c>Skilled Worker</c>.</summary>
    public const int MaxVisaCategory = 120;

    /// <summary>Requirements one advisory may list.</summary>
    public const int MaxVisaRequirements = 40;

    /// <summary>Required documents one advisory may list.</summary>
    public const int MaxVisaDocuments = 40;

    /// <summary>Characters in one requirement or document line.</summary>
    public const int MaxVisaLine = 300;

    /// <summary>Characters in the free-text processing time — "10 to 15 working days".</summary>
    public const int MaxProcessingTime = 150;

    // ── Public holidays ─────────────────────────────────────────────────────────────────────

    /// <summary>Characters in a holiday name.</summary>
    public const int MaxHolidayName = 200;

    /// <summary>Characters in a holiday description.</summary>
    public const int MaxHolidayDescription = 1_000;

    /// <summary>Characters in the region a regional holiday applies to.</summary>
    public const int MaxRegion = 150;

    // ── Paging and dashboards ───────────────────────────────────────────────────────────────

    /// <summary>Rows in one page of an intelligence listing.</summary>
    public const int MaxPageSize = 50;

    public const int DefaultPageSize = 20;

    /// <summary>Notices one dashboard band carries. A dashboard is a summary, not a browse.</summary>
    public const int DashboardBandSize = 5;

    /// <summary>
    /// Days ahead a country dashboard looks for holidays.
    /// <para>
    /// Ninety, because the question a dashboard answers is "should I plan around anything" and the
    /// horizon a member plans an appointment over is a quarter. A year of holidays on a dashboard is
    /// a calendar nobody reads.
    /// </para>
    /// </summary>
    public const int DashboardHolidayHorizonDays = 90;

    /// <summary>Characters in a search phrase.</summary>
    public const int MaxSearchTerm = 200;
}
