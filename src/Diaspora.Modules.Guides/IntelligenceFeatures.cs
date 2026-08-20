namespace Diaspora.Modules.Guides;

/// <summary>
/// The feature gates the Country Intelligence Platform owns.
/// <para>
/// <b>Three, and all staff-only.</b> Gates exist so a client can grey out a control with a reason
/// instead of offering it and failing — and the only controls in this phase a client might offer and
/// be refused are the editorial ones. Reading a country's official information needs no gate at all,
/// because it needs no account.
/// </para>
/// <para>
/// <b>Publishing is a separate gate from editing, unlike B8.1's two.</b> The editorial screen and the
/// publish button are frequently in front of different people, and a client that could only ask "may
/// this person touch intelligence at all" would either show a publish button that fails or hide an
/// editing screen from somebody entitled to it.
/// </para>
/// <para>
/// All three sit at <c>AccessTier.Limited</c> with the staff flag set, which is how the platform
/// expresses "not a progression": a gate below Limited is granted to phone-unverified accounts, and a
/// gate at Verified would tell a member that verifying their identity unlocks the ability to publish
/// government announcements. It does not, and it must not.
/// </para>
/// </summary>
public static class IntelligenceFeatures
{
    /// <summary>Draft and edit official information. Staff only.</summary>
    public const string Manage = "guides.intelligence.manage";

    /// <summary>Verify and publish official notices. Staff only.</summary>
    public const string Publish = "guides.intelligence.publish";

    /// <summary>Read country intelligence usage figures. Staff only.</summary>
    public const string Analytics = "guides.intelligence.analytics";
}
