namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// Where a reservation stands. The brief's reservation lifecycle, exactly.
/// <para>
/// <b>Three of the five terminal states return the stock, and that is the invariant the whole
/// aggregate exists to protect.</b> <see cref="Cancelled"/>, <see cref="Expired"/> and a rejected
/// hand-off all put the units back on the listing; only <see cref="Completed"/> converts them to
/// sold. A reservation that ended without doing one or the other is stock nobody can buy and
/// nobody owns.
/// </para>
/// </summary>
public enum ReservationStatus
{
    /// <summary>Requested, stock held, seller not yet acknowledged.</summary>
    Pending = 0,

    /// <summary>Agreed, waiting for the buyer to collect or the seller to hand over.</summary>
    AwaitingCollection = 1,

    /// <summary>Handed over. The only state that turns held stock into sold stock.</summary>
    Completed = 2,

    /// <summary>Called off by either party. Stock returns.</summary>
    Cancelled = 3,

    /// <summary>Nobody collected in time. Stock returns.</summary>
    Expired = 4
}
