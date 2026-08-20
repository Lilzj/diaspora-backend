namespace Diaspora.Modules.Housing.Domain.Enums;

/// <summary>
/// Where a hold on a property has got to.
/// <para>
/// <b>Every exit releases the units, and there are exactly four.</b> Occupation converts held
/// units to occupied; cancellation, refusal and expiry return them. There is no fifth, because a
/// fifth would be a room that is neither on the market nor lived in — which is how a housing
/// platform quietly loses a property.
/// </para>
/// <para>
/// <b><see cref="LeaseSigned"/> records that the parties say they have signed something. It does
/// not model the document.</b> Lease management and digital signatures are explicitly out of
/// scope; this is a milestone in a workflow, and the deliberate boundary between what Housing
/// tracks and what a future contracts module would own.
/// </para>
/// </summary>
public enum HousingReservationStatus
{
    /// <summary>The member has asked. Units are already held — see the aggregate for why.</summary>
    Requested = 0,

    /// <summary>The landlord has agreed to hold it.</summary>
    Reserved = 1,

    /// <summary>The parties report an agreement. Housing stores the fact, never the document.</summary>
    LeaseSigned = 2,

    /// <summary>Moved in. The successful terminal state.</summary>
    Occupied = 3,

    /// <summary>Called off by either party. Units return.</summary>
    Cancelled = 4,

    /// <summary>The landlord refused. Units return.</summary>
    Declined = 5,

    /// <summary>The hold ran out. The sweep's terminal state, and units return.</summary>
    Expired = 6
}
