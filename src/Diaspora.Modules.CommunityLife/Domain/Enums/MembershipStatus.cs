namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// Where one member stands with one organization.
/// <para>
/// <b><see cref="Left"/> and <see cref="Removed"/> are different states rather than one.</b> A
/// member who walked away may rejoin; a member an administrator removed may not, without that
/// administrator changing their mind. Collapsing them would make the second silently reversible by
/// the person it was applied to.
/// </para>
/// </summary>
public enum MembershipStatus
{
    /// <summary>Asked to join an organization whose policy is to approve requests.</summary>
    Requested = 0,

    /// <summary>Invited by an administrator and has not yet answered.</summary>
    Invited = 1,

    /// <summary>In.</summary>
    Active = 2,

    /// <summary>The request was refused.</summary>
    Rejected = 3,

    /// <summary>Walked away. May come back.</summary>
    Left = 4,

    /// <summary>Removed by an administrator. Coming back needs their agreement.</summary>
    Removed = 5,

    /// <summary>Membership lapsed — a renewal window passed. May be reinstated.</summary>
    Lapsed = 6
}
