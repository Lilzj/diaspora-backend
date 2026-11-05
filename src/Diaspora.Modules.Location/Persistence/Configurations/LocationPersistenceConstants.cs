namespace Diaspora.Modules.Location.Persistence.Configurations;

/// <summary>
/// Column widths and table names, in one place.
/// <para>
/// Shared so that a length declared on the domain's guard and the length declared on the
/// column cannot drift apart — a mismatch there is a truncation exception in production and
/// nowhere else.
/// </para>
/// </summary>
public static class LocationPersistenceConstants
{
    public const string CountriesTable = "countries";
    public const string AreasTable = "administrative_areas";
    public const string ProfilesTable = "location_profiles";
    public const string SavedLocationsTable = "saved_locations";
    public const string RecentLocationsTable = "recent_locations";
    public const string ExperiencesTable = "country_experiences";
    public const string ExpertiseTable = "country_expertise";
    public const string VerifiedResidenciesTable = "verified_residencies";

    /// <summary>21 Oct 2026. The "when the app is open" setting and the last area — no history.</summary>
    public const string MemberPresenceTable = "member_presence";

    /// <summary>5 Nov 2026. The world list of towns (GeoNames), for reading a place out of a search.</summary>
    public const string WorldTownsTable = "world_towns";

    public const int TownNameLength = 200;
    public const int TownSearchNamesLength = Domain.Towns.TownNames.MaxSearchNamesLength;

    public const int CountryCodeLength = 2;
    public const int CurrencyCodeLength = 3;
    public const int LanguageCodeLength = 2;
    public const int LocaleLength = 8;
    public const int CallingCodeLength = 8;
    public const int TimeZoneLength = 64;
    public const int NameLength = 160;
    public const int SlugLength = 120;
    public const int PathLength = 1024;
    public const int PostalCodeLength = 16;
    public const int LabelLength = 40;
    public const int RoleLength = 60;
    public const int ExpertiseAreaLength = 60;
    public const int OverviewLength = 2000;
    public const int UrlLength = 512;
    public const int ReasonLength = 256;
    public const int ContinentLength = 40;

    /// <summary>
    /// A ring at the domain's 2,000-vertex cap packed as <c>lat,lon;</c> pairs. Sized so the
    /// column never silently truncates an outline that the domain accepted.
    /// </summary>
    public const int BoundaryLength = 60000;

    /// <summary>Packed language and time-zone lists on a country row.</summary>
    public const int PackedListLength = 512;
}
