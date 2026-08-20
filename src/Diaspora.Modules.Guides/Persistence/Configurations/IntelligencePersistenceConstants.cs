namespace Diaspora.Modules.Guides.Persistence.Configurations;

/// <summary>
/// Table names and column widths every Country Intelligence configuration shares.
/// <para>
/// <b>Every table name is <c>intel_</c>-prefixed, and the prefix is not decoration.</b> SQLite —
/// which the integration suite runs on — ignores schemas entirely, so two modules whose tables differ
/// only by schema collide there and nowhere else, which is the worst possible place for a name clash
/// to first appear. <c>categories</c>, <c>authorities</c>, <c>embassies</c> and <c>holidays</c> are
/// all names a later module will want. B6, B7, B8 and B8.1 each record the same trap.
/// </para>
/// <para>
/// These sit in the <c>guides</c> schema alongside B8's and B8.1's tables, because B8.2 is the same
/// module: one schema, one database contributor, one owner. A second schema would suggest a second
/// owner of country knowledge, and the whole architectural claim of this phase is that there is one.
/// </para>
/// </summary>
internal static class IntelligencePersistenceConstants
{
    public const string NoticesTable = "intel_notices";
    public const string NoticeLocalizationsTable = "intel_notice_localizations";
    public const string NoticeAttachmentsTable = "intel_notice_attachments";
    public const string NoticeReferencesTable = "intel_notice_references";
    public const string NoticeLanguagesTable = "intel_notice_languages";

    public const string NoticeCategoriesTable = "intel_notice_categories";
    public const string NoticeCategoryLocalizationsTable = "intel_notice_category_localizations";

    public const string AuthoritiesTable = "intel_authorities";

    public const string EmbassiesTable = "intel_embassies";
    public const string EmbassyOfficeHoursTable = "intel_embassy_office_hours";
    public const string EmbassyServicesTable = "intel_embassy_services";
    public const string EmbassyLocalizationsTable = "intel_embassy_localizations";

    public const string EmergencyDirectoriesTable = "intel_emergency_directories";
    public const string EmergencyContactsTable = "intel_emergency_contacts";
    public const string EmergencyContactLocalizationsTable = "intel_emergency_contact_localizations";

    public const string VisaAdvisoriesTable = "intel_visa_advisories";
    public const string VisaLinesTable = "intel_visa_lines";
    public const string VisaGuidesTable = "intel_visa_guides";
    public const string VisaLocalizationsTable = "intel_visa_localizations";

    public const string PublicHolidaysTable = "intel_public_holidays";
    public const string PublicHolidayLocalizationsTable = "intel_public_holiday_localizations";

    public const string CountryAnalyticsTable = "intel_country_analytics";
    public const string CountryCountersTable = "intel_country_counters";

    /// <summary>Enum names stored as text. Matches B8's and B8.1's width so none of them disagree.</summary>
    public const int EnumLength = 32;

    /// <summary>ISO 3166-1 alpha-2.</summary>
    public const int CountryCodeLength = 2;

    /// <summary>A counter kind — <c>notice-viewed</c>. Open-ended, so generously sized.</summary>
    public const int CounterKindLength = 64;
}
