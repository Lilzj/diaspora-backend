namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// One move in a negotiation, as the history records it.
/// <para>
/// Separate from <see cref="OfferStatus"/> because the two answer different questions: the status
/// is where the negotiation is now, and this is what somebody did. A negotiation that ends
/// Rejected has a history of Offered, Countered, Countered, Rejected — and the brief's
/// "maintain complete negotiation history" is exactly the difference between keeping that list
/// and keeping only the last value.
/// </para>
/// </summary>
public enum NegotiationAction
{
    /// <summary>The opening offer.</summary>
    Offered = 0,

    /// <summary>A different number, from the other side.</summary>
    Countered = 1,

    Accepted = 2,

    Rejected = 3,

    Withdrawn = 4,

    /// <summary>The clock ran out. Recorded by the sweep, attributed to nobody.</summary>
    Expired = 5
}
