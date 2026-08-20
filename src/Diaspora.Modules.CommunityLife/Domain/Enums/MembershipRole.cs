namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// What a member may do inside an organization.
/// <para>
/// <b>Ordered by authority, and the order is load-bearing</b> — every permission check in the
/// module is a comparison against a minimum, so adding a role between two existing ones is a
/// deliberate act rather than an append. The numbering leaves gaps for exactly that reason.
/// </para>
/// <para>
/// This is not a platform role and never becomes one. It says what somebody may do inside one
/// organization; whether they may do anything at all is the platform's permission model, and an
/// owner of a suspended organization is still an owner and still cannot publish.
/// </para>
/// </summary>
public enum MembershipRole
{
    /// <summary>Belongs, and that is all. The default.</summary>
    Member = 0,

    /// <summary>Signed up to help at events. Can take attendance, and nothing else.</summary>
    Volunteer = 10,

    /// <summary>Handles reports and content within the organization.</summary>
    Moderator = 20,

    /// <summary>Creates and runs events on the organization's behalf.</summary>
    Organizer = 30,

    /// <summary>Manages the page, the members and the events.</summary>
    Administrator = 40,

    /// <summary>Created it, or had it handed to them. Exactly one at a time.</summary>
    Owner = 50
}
