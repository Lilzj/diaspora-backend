namespace Diaspora.Application.Identity.Models;

/// <summary>
/// The wire a one-time code travels on.
/// </summary>
/// <remarks>
/// Reported by <see cref="Abstractions.IOtpDeliveryService"/> so a use case can tell the member
/// where to look without guessing. Recovery is the case that needs it: the acknowledgement has to
/// name a channel, and naming the wrong one leaves the member watching an inbox nothing is coming
/// to.
/// </remarks>
public enum OtpDeliveryChannel
{
    /// <summary>No transport is configured. Nothing will arrive anywhere.</summary>
    None = 0,

    Sms = 1,

    Email = 2
}
