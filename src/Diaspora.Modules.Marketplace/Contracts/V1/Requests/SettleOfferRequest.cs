namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Ends a negotiation.
/// <para>
/// One request for the three endings a person can cause, because they differ only in which word
/// the caller sends and in who is allowed to send it. Accepting and declining are the turn-holder's
/// to do; withdrawing is the mover's.
/// </para>
/// </summary>
public sealed record SettleOfferRequest
{
    /// <summary><c>Accept</c>, <c>Reject</c> or <c>Withdraw</c>.</summary>
    public required string Decision { get; init; }

    /// <summary>Why, when the caller wants to say. Shown to the other party.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// On an acceptance, when the buyer will collect. Ignored otherwise. Supplying it here saves
    /// the accepting party a second call to arrange the hand-off they have just agreed to.
    /// </summary>
    public DateTimeOffset? CollectFrom { get; init; }

    public DateTimeOffset? CollectUntil { get; init; }

    public string? CollectionPlace { get; init; }
}
