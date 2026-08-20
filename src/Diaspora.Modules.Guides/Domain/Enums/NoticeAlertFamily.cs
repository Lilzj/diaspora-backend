namespace Diaspora.Modules.Guides.Domain.Enums;

/// <summary>
/// Which extra published contract a category's notices produce when they go live.
/// <para>
/// <b>This is why notice categories are a seeded aggregate rather than an enum.</b> The brief asks
/// for three publication contracts — a general one, an emergency alert and a government
/// announcement — and something has to decide which a given notice is. Hard-coding a list of
/// category keys inside the relay would mean a deployment adding "Cyclone Warnings" gets a routine
/// notice for a storm, and would only find out during one. Declaring it on the category makes the
/// decision editorial, visible in the catalogue, and changeable without a release.
/// </para>
/// <para>
/// Every notice publishes <c>OfficialNoticePublished</c> regardless. This adds a second contract on
/// top; it never replaces the first, because a consumer that subscribed to "all official notices"
/// must not silently stop receiving the most important ones.
/// </para>
/// </summary>
public enum NoticeAlertFamily
{
    /// <summary>Ordinary intelligence. One contract, and it is enough.</summary>
    None = 0,

    /// <summary>
    /// Something happening now that a member may need to act on — an evacuation, an outbreak, a
    /// border closure. Publishes <c>EmergencyAlertPublished</c> as well.
    /// </summary>
    Emergency = 1,

    /// <summary>
    /// A formal statement from government. Publishes <c>GovernmentAnnouncementPublished</c> as well.
    /// </summary>
    GovernmentAnnouncement = 2
}
