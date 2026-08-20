namespace Diaspora.Modules.Financial.Domain.Enums;

/// <summary>Whether a purchased placement is currently doing anything.</summary>
public enum PlacementStatus
{
    /// <summary>Bought, starts later.</summary>
    Scheduled = 1,

    Active = 2,

    Expired = 3,

    Cancelled = 4,

    /// <summary>Cancelled and the money returned.</summary>
    Refunded = 5,

    /// <summary>
    /// Stopped by the buyer, with the clock stopped too.
    /// <para>
    /// <b>Paused is not cancelled, and the difference is the member's money.</b> Cancelling and
    /// re-buying loses whatever remains of a paid window; a pause banks it and hands it back on
    /// resume, which is why resuming extends <c>Window.EndsAt</c> by exactly the paused duration
    /// rather than restarting the clock.
    /// </para>
    /// </summary>
    Paused = 6
}
