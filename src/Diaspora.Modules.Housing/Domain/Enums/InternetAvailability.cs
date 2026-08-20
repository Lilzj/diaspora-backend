namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// What connectivity a neighbourhood or a property has.
/// <para>
/// Its own enum rather than a <see cref="NeighborhoodRating"/> band, because the question people
/// actually ask is not "is the internet good" but "can I hold a video call for my job". The
/// values are the answers to that.
/// </para>
/// </summary>
public enum InternetAvailability
{
    Unknown = 0,
    None = 1,
    MobileOnly = 2,
    Basic = 3,
    Standard = 4,
    Fast = 5,
    Gigabit = 6
}
