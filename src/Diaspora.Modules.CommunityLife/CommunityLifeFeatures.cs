namespace Diaspora.Modules.CommunityLife;

/// <summary>
/// The feature gates the Community Life module owns.
/// <para>
/// Dot-separated names with the module prefix. Every gate's floor is at least
/// <c>AccessTier.Limited</c> — a gate below Limited is granted to phone-unverified accounts, which
/// is the B6 lesson made policy. Reading published events and organization pages needs no gate at
/// all: somebody who has just landed in a city should be able to find a church service before they
/// have finished signing up for anything.
/// </para>
/// <para>
/// <b><see cref="OrganizationCreate"/> is a name the host already holds.</b>
/// <c>organizations.create</c> has been declared provisionally since B2 and the mobile client
/// renders against it. B12 claims it at its original Verified floor and deletes the provisional
/// entry in the same change — because the feature catalogue refuses two modules declaring one gate,
/// and the resulting startup failure takes down every request that resolves it, phone verification
/// included. B8, B9 and B10 each recorded that trap; this is the first phase since B7 to walk into
/// it deliberately rather than to find nothing to claim.
/// </para>
/// </summary>
public static class CommunityLifeFeatures
{
    /// <summary>Browse and search events and organizations. The entry point to the module.</summary>
    public const string Browse = "community-life.browse";

    /// <summary>
    /// The gate the host has held provisionally since B2, claimed unchanged. Verified only,
    /// because an organization page is an institution's public face and members will trust it.
    /// </summary>
    public const string OrganizationCreate = "organizations.create";

    /// <summary>
    /// Publish an event. Verified only: an event puts strangers in a room together at a named
    /// address and a time.
    /// </summary>
    public const string EventCreate = "community-life.events.create";

    /// <summary>
    /// Say you are coming. Limited, deliberately — an RSVP is how a newcomer finds their people,
    /// and it costs nobody anything.
    /// </summary>
    public const string Rsvp = "community-life.rsvp";

    /// <summary>Join an organization. Limited; the organization's own policy decides the rest.</summary>
    public const string Membership = "community-life.membership";

    /// <summary>Take attendance at the door. Held by organizers and their volunteers.</summary>
    public const string CheckIn = "community-life.checkin";

    /// <summary>Work the community-life moderation queue. Staff only.</summary>
    public const string Moderate = "community-life.moderate";
}
