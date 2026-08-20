namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// Where a community-submitted neighbourhood observation has got to.
/// <para>
/// <b>Submitted is not visible, and that is the point of the whole aggregate.</b> "Frequent power
/// outages" and "popular among Nigerians" are both things a member will say about a place; the
/// first is useful and the second is a sentence a moderator needs to read before the platform
/// publishes it about somebody's home. Insights are moderated by default and nothing renders an
/// unapproved one.
/// </para>
/// </summary>
public enum NeighborhoodInsightStatus
{
    /// <summary>Waiting for a moderator. Visible to its author and to staff only.</summary>
    Submitted = 0,

    /// <summary>Published on the neighbourhood profile.</summary>
    Approved = 1,

    /// <summary>Refused, with a reason the author can read.</summary>
    Rejected = 2,

    /// <summary>Was approved, and has since stopped being true. Kept, not deleted.</summary>
    Retired = 3
}
