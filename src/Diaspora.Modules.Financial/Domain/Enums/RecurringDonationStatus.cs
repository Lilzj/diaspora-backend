namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>
/// Where a standing donation instruction is in its life.
/// <para>
/// <b>Five states, and the two failure ones are separate on purpose.</b> A card that declined once
/// is <see cref="Retrying"/> and will be tried again; a card that has declined its way through the
/// retry budget is <see cref="Suspended"/> and will not be, until the donor does something. Folding
/// them into one "failed" would mean either giving up after one decline or retrying for ever, and
/// both are wrong — the first loses the donation, the second is a platform hammering somebody's bank
/// every night.
/// </para>
/// </summary>
public enum RecurringDonationStatus
{
    /// <summary>Running. The next charge is scheduled.</summary>
    Active = 0,

    /// <summary>The last attempt failed and another is scheduled.</summary>
    Retrying = 1,

    /// <summary>The retry budget is spent. Nothing further will be attempted.</summary>
    Suspended = 2,

    /// <summary>The donor stopped it. Terminal.</summary>
    Cancelled = 3,

    /// <summary>
    /// The cause it gave to is closed, so there is nothing left to give to. Terminal.
    /// <para>
    /// Distinct from cancelled because the donor did not ask for it, and the two say materially
    /// different things on a screen.
    /// </para>
    /// </summary>
    Completed = 4
}
