namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// How somebody gets into an organization.
/// <para>
/// Configurable per organization because the answer genuinely differs: a church admits anyone who
/// walks in, an alumni association checks you actually went there, and a professional body has a
/// committee. A platform that picked one would be wrong for the other two.
/// </para>
/// </summary>
public enum MembershipPolicyKind
{
    /// <summary>Anyone may join, immediately.</summary>
    Open = 0,

    /// <summary>Anyone may ask; an administrator decides.</summary>
    RequestToJoin = 1,

    /// <summary>Only people an administrator invites.</summary>
    InviteOnly = 2,

    /// <summary>Nobody, for now. The page stays; the door is shut.</summary>
    Closed = 3
}
