namespace Diaspora.Modules.Location.Domain.Enums;

/// <summary>
/// How deeply a member knows a country, derived from how long they have lived there.
/// <para>
/// Derived rather than declared, on purpose. A self-assessed expertise level is a claim
/// nobody can check and everyone inflates; time in country is a fact the member states once
/// and the calendar maintains. <see cref="Domain.Experience.CountryExperience"/> computes
/// this from the residence period, so it cannot be set directly.
/// </para>
/// </summary>
public enum ExperienceLevel
{
    /// <summary>Under six months. Still finding the supermarket.</summary>
    Newcomer = 0,

    /// <summary>Six months to two years.</summary>
    Settling = 1,

    /// <summary>Two to five years.</summary>
    Established = 2,

    /// <summary>Five to ten years.</summary>
    Seasoned = 3,

    /// <summary>Over ten years, or born there.</summary>
    Native = 4
}
