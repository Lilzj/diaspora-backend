namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// Where a listing sits in its life.
/// <para>
/// <b>Published and Available are two states rather than one, and the separation is the whole
/// reason this enum is not the marketplace's.</b> A landlord publishes a listing so people can
/// find it; whether it can be moved into is a different question, answered by whether the
/// available-from date has arrived and units remain. A flat advertised in March for a June
/// tenancy is published and not yet available, and collapsing the two would either hide it for
/// three months or claim it can be occupied tomorrow.
/// </para>
/// <para>
/// <b>Unavailable is reversible and Archived is not.</b> Unavailable is "off the market for now"
/// — the landlord is redecorating, or the tenancy is being renewed — and it returns to Available
/// without being re-published. Archived is the end of the listing's public life and needs a
/// deliberate restore.
/// </para>
/// </summary>
public enum HousingListingStatus
{
    /// <summary>Being written. Visible to its landlord and to staff, and to nobody else.</summary>
    Draft = 0,

    /// <summary>Published and discoverable, but not yet ready to be moved into.</summary>
    Published = 1,

    /// <summary>Discoverable and ready to be occupied. The state most listings live in.</summary>
    Available = 2,

    /// <summary>Held for a named member. Still visible, no longer reservable.</summary>
    Reserved = 3,

    /// <summary>Somebody lives there. The successful terminal state of a tenancy.</summary>
    Occupied = 4,

    /// <summary>Temporarily off the market. Returns to Available without republication.</summary>
    Unavailable = 5,

    /// <summary>Withdrawn. Retained for the landlord's record and for analytics.</summary>
    Archived = 6
}
