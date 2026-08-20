namespace Diaspora.Modules.CommunityProgramme;

/// <summary>
/// The feature gates the Community Programme module owns.
/// <para>
/// Dot-separated names with the module prefix. Every gate's floor is at least
/// <c>AccessTier.Limited</c>: a gate below Limited is granted to phone-unverified accounts, which is
/// the B6 lesson made policy.
/// </para>
/// <para>
/// <b>Four gates rather than one, because a deployment may want them separately.</b> A country
/// launching with events and no paid memberships turns <see cref="Memberships"/> off and leaves
/// <see cref="Polls"/> on; a deployment with no volunteering programme turns
/// <see cref="Volunteering"/> off without touching the rest. One gate for the whole module would
/// make each of those an all-or-nothing decision.
/// </para>
/// </summary>
public static class CommunityProgrammeFeatures
{
    private const string Prefix = "communities";

    /// <summary>Membership programmes, tiers, applications and renewals.</summary>
    public const string Memberships = Prefix + ".memberships";

    /// <summary>Volunteer opportunities, applications, shifts, hours and certificates.</summary>
    public const string Volunteering = Prefix + ".volunteering";

    /// <summary>Community polls and their results.</summary>
    public const string Polls = Prefix + ".polls";

    /// <summary>Scoped announcements.</summary>
    public const string Announcements = Prefix + ".announcements";
}
