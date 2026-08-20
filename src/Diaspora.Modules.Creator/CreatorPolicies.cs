namespace Diaspora.Modules.Creator;

/// <summary>
/// The authorisation policy names the module's commands and queries reference.
/// <para>
/// Dot-separated, mirroring <see cref="CreatorPermissions"/> one-for-one. The kernel validates at
/// startup that each policy names a permission that exists, so a typo fails the boot rather than
/// silently denying a route for ever.
/// </para>
/// </summary>
public static class CreatorPolicies
{
    private const string Prefix = "creator";

    public const string ProfileRead = Prefix + ".profile.read";
    public const string ProfileManage = Prefix + ".profile.manage";
    public const string Participate = Prefix + ".programme.participate";
    public const string VerificationReview = Prefix + ".verification.review";
    public const string GrantReview = Prefix + ".grant.review";
    public const string ProgrammeManage = Prefix + ".programme.manage";
}
