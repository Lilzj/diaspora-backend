namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>Who can reach an organization page, independent of its lifecycle state.</summary>
public enum OrganizationVisibility
{
    /// <summary>Administrators only. Where every organization starts.</summary>
    Private = 0,

    /// <summary>Anyone, signed in or not.</summary>
    Public = 1,

    /// <summary>Signed-in members only.</summary>
    MembersOnly = 2,

    /// <summary>Reachable by direct link but absent from search and browse.</summary>
    Unlisted = 3
}
