namespace Diaspora.Modules.CommunityLife.Domain.Enums;

/// <summary>
/// How a page of events is ordered.
/// <para>
/// <see cref="Soonest"/> is the default and that is not arbitrary. Every other catalogue on this
/// platform defaults to newest-first because a listing does not expire; an event does, and a member
/// looking at a list of events wants the one they can still get to.
/// </para>
/// </summary>
public enum EventSortOrder
{
    /// <summary>Starting soonest first. The default.</summary>
    Soonest = 0,

    /// <summary>Most recently announced first.</summary>
    Newest = 1,

    /// <summary>Most RSVPs first.</summary>
    MostPopular = 2,

    /// <summary>Nearest to the search centre first. Needs a resolved point.</summary>
    Nearest = 3,

    /// <summary>Whatever the registered ranking strategy says.</summary>
    Relevance = 4
}
