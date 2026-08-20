namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// What an insight is about.
/// <para>
/// Fixed rather than configurable, unlike accommodation types and amenities, and deliberately so:
/// this is the axis a moderation queue is filtered by and a profile is grouped by, and a
/// free-form or contributor-extended set would make both unenumerable. The brief's examples all
/// land inside it.
/// </para>
/// </summary>
public enum NeighborhoodInsightCategory
{
    Safety = 0,
    Transport = 1,
    CostOfLiving = 2,
    Community = 3,
    Noise = 4,
    Utilities = 5,
    Amenities = 6,
    Schools = 7,
    Nightlife = 8,
    Accessibility = 9,
    Healthcare = 10,
    Other = 99
}
