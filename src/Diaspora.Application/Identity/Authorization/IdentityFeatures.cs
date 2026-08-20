namespace Diaspora.Application.Identity.Authorization;

/// <summary>
/// The feature gates the Identity module owns.
/// <para>
/// B2 declared the whole platform's gates here — <c>marketplace.listings.create</c>,
/// <c>jobs.apply</c>, <c>guides.publish</c>, <c>messaging.send</c> — for modules that had
/// not been written. Identity had no way to know when those gates should open and no way
/// to enforce them; it just held the strings. They now live with their owners, reaching
/// the catalogue through <c>IFeatureContributor</c> like everything else.
/// </para>
/// <para>
/// Constants rather than an enum: gate names cross process and language boundaries as
/// strings, and an enum would force a client redeploy whenever a module added a gate.
/// </para>
/// </summary>
public static class IdentityFeatures
{
    /// <summary>Manage your own profile. Open from limited access.</summary>
    public const string ManageOwnProfile = "profile.manage";

    /// <summary>See and revoke your own sessions.</summary>
    public const string ManageOwnSessions = "sessions.manage";

    /// <summary>Register and manage your own devices.</summary>
    public const string ManageOwnDevices = "devices.manage";

    /// <summary>Reach the Verification Center.</summary>
    public const string AccessVerificationCenter = "verification.center.access";

    /// <summary>Submit a verification request. Closes once one is in flight.</summary>
    public const string SubmitVerification = "verification.submit";

    /// <summary>Moderate content. Staff only.</summary>
    public const string ModerateContent = "moderation.content";

    /// <summary>Administer accounts. Staff only.</summary>
    public const string ManageUsers = "administration.users";

    public static readonly IReadOnlyList<string> All =
    [
        ManageOwnProfile,
        ManageOwnSessions,
        ManageOwnDevices,
        AccessVerificationCenter,
        SubmitVerification,
        ModerateContent,
        ManageUsers
    ];
}
