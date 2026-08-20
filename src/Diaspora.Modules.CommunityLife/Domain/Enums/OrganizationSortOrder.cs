namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>How a page of organizations is ordered.</summary>
public enum OrganizationSortOrder
{
    /// <summary>Most members first. The default: a directory should lead with the living ones.</summary>
    MostMembers = 0,

    /// <summary>Most recently published first.</summary>
    Newest = 1,

    /// <summary>Alphabetical. The one a member uses when they know the name.</summary>
    Name = 2,

    /// <summary>Most events in the last quarter first.</summary>
    MostActive = 3,

    /// <summary>Nearest to the search centre first. Needs a resolved point.</summary>
    Nearest = 4
}
