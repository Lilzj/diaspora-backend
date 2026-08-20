namespace Diaspora.Modules.Marketplace.Domain.Enums;

/// <summary>
/// Where a negotiation stands.
/// <para>
/// <b><see cref="Countered"/> is a live state, not a terminal one.</b> A counter is the other
/// party's turn, so the negotiation is still open and the whole point of the aggregate is that
/// the sequence of counters is kept. Only Accepted, Rejected, Withdrawn and Expired end it, and
/// none of the four can be left.
/// </para>
/// </summary>
public enum OfferStatus
{
    /// <summary>Awaiting the seller.</summary>
    Pending = 0,

    /// <summary>Answered with a different number. Awaiting whoever did not counter last.</summary>
    Countered = 1,

    /// <summary>Agreed. The one terminal state that reserves stock.</summary>
    Accepted = 2,

    /// <summary>Declined by the party who was asked.</summary>
    Rejected = 3,

    /// <summary>Taken back by the party who made it.</summary>
    Withdrawn = 4,

    /// <summary>Nobody answered in time.</summary>
    Expired = 5
}
