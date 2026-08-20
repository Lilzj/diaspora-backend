namespace Diaspora.Modules.CommunityLife.Extensibility.Catalog;

/// <summary>
/// The facet names the brief asks the search to answer.
/// <para>
/// Constants rather than an enum, because a contributor supplies them as strings and an enum would
/// make the seam closed exactly where it is meant to be open. These four are simply the ones the
/// brief names and the shipped catalogue uses; a deployment may invent others and filter on them,
/// and nothing here has to change.
/// </para>
/// </summary>
public static class CommunityLifeSearchFacets
{
    /// <summary>Church services, prayer meetings, mosque gatherings, temple festivals.</summary>
    public const string Religious = "religious";

    /// <summary>Festivals, independence celebrations, food festivals, concerts.</summary>
    public const string Cultural = "cultural";

    /// <summary>Networking, career fairs, seminars, conferences.</summary>
    public const string Business = "business";

    /// <summary>Orientations, student socials, university society events.</summary>
    public const string Student = "student";

    /// <summary>Charity drives, clean-ups, volunteer activities.</summary>
    public const string Volunteer = "volunteer";

    /// <summary>Matches, competitions, training sessions.</summary>
    public const string Sport = "sport";

    /// <summary>Weddings, naming ceremonies, funerals, birthdays.</summary>
    public const string Family = "family";
}
