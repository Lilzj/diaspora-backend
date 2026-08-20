namespace Diaspora.Modules.CommunityProgramme;

/// <summary>
/// The authorisation policy names the module's endpoints and commands reference.
/// <para>
/// Dot-separated, mirroring <see cref="CommunityProgrammePermissions"/> one-for-one. The kernel
/// validates at startup that each policy names a permission that exists, so a typo fails the boot
/// rather than silently denying a route for ever.
/// </para>
/// </summary>
public static class CommunityProgrammePolicies
{
    private const string Prefix = "communities";

    public const string Read = Prefix + ".programme.read";
    public const string Participate = Prefix + ".programme.participate";
    public const string ManageProgramme = Prefix + ".programme.manage";
    public const string ManageVolunteers = Prefix + ".volunteer.manage";
    public const string ManagePolls = Prefix + ".poll.manage";
    public const string PublishAnnouncements = Prefix + ".announcement.publish";
    public const string ViewAnalytics = Prefix + ".analytics.view";
}
