namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// Where a property visit has got to.
/// <para>
/// <b>A viewing is a separate aggregate from a reservation, and this enum is why.</b> Most
/// viewings never become reservations, and most of the ones that do involve several viewings by
/// several people first. Folding the visit into the hold would mean either a reservation per
/// enquiry — which would take the property off the market for every browser — or no record of
/// who came round at all.
/// </para>
/// </summary>
public enum HousingViewingStatus
{
    /// <summary>A member has asked to see it. The landlord has not answered yet.</summary>
    Requested = 0,

    /// <summary>A time is agreed.</summary>
    Scheduled = 1,

    /// <summary>The visit happened. The state a reservation may follow from.</summary>
    Completed = 2,

    /// <summary>Called off by either party.</summary>
    Cancelled = 3,

    /// <summary>The time came and nobody turned up. Recorded, because a pattern of it matters.</summary>
    NoShow = 4,

    /// <summary>The requested slot passed without an answer. The sweep's terminal state.</summary>
    Expired = 5
}
