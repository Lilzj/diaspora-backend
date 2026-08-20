namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// Where a listing sits in its life.
/// <para>
/// <b><see cref="Published"/> and <see cref="Active"/> are genuinely different states, and
/// collapsing them is the mistake this enum exists to avoid.</b> A shop that wants tomorrow's
/// drop visible today, and a seller who wants a car advertised before they will answer about it,
/// are both doing something ordinary. Published means "you can see it"; Active means "you can
/// commit to it". Only Active takes offers and reservations.
/// </para>
/// <para>
/// <b>Restored is not a state.</b> The brief's lifecycle ends "Archived → Restored", and restore
/// is a transition rather than a place: an archived listing restores to <see cref="Draft"/>, and
/// a soft-deleted one restores to whatever it held. Modelling "Restored" as a status would leave
/// a listing permanently labelled by the last administrative act performed on it.
/// </para>
/// </summary>
public enum ListingStatus
{
    /// <summary>Being written. Visible only to the seller.</summary>
    Draft = 0,

    /// <summary>Visible, but not yet taking offers or reservations.</summary>
    Published = 1,

    /// <summary>Live and transacting. The state nearly every listing spends its life in.</summary>
    Active = 2,

    /// <summary>Every unit is held for a named buyer. Still visible, no longer available.</summary>
    Reserved = 3,

    /// <summary>Sold out. Terminal but for archival.</summary>
    Sold = 4,

    /// <summary>Its expiry passed. Reversible — extending the date brings it back.</summary>
    Expired = 5,

    /// <summary>Retired from every public surface by the seller. Reversible.</summary>
    Archived = 6
}
