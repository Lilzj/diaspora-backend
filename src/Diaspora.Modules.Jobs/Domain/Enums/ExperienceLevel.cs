namespace Diaspora.Modules.Jobs.Domain.Enums;

/// <summary>
/// The seniority a posting is pitched at, and the level a candidate places themselves at.
/// <para>
/// Ordered and comparable, so "this role or more junior" is a range rather than a set. The
/// values are spaced by intent rather than by years: a decade in one market and a decade in
/// another buy very different titles, and a ladder denominated in years would quietly penalise
/// exactly the members this platform exists for.
/// </para>
/// </summary>
public enum ExperienceLevel
{
    /// <summary>No prior experience expected. Internships and apprenticeships.</summary>
    Entry = 1,

    Junior = 2,

    Mid = 3,

    Senior = 4,

    /// <summary>Leads a team or a workstream.</summary>
    Lead = 5,

    /// <summary>Sets technical or professional direction.</summary>
    Principal = 6,

    Director = 7,

    Executive = 8
}
