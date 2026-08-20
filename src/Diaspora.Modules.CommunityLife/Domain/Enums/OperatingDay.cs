namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// A day in an organization's operating week.
/// <para>
/// Its own enum rather than <see cref="System.DayOfWeek"/>, and for one reason:
/// <see cref="PublicHoliday"/>. Every church, mosque and community centre has a different answer
/// for the days the country stops, and a schema that could only say Monday-to-Sunday would force
/// that answer into free text where nothing can read it.
/// </para>
/// </summary>
public enum OperatingDay
{
    Monday = 1,
    Tuesday = 2,
    Wednesday = 3,
    Thursday = 4,
    Friday = 5,
    Saturday = 6,
    Sunday = 7,

    /// <summary>Whatever the country's calendar says is a holiday.</summary>
    PublicHoliday = 8
}
