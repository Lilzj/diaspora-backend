namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>
/// Answers a negotiation with a different number.
/// <para>
/// Either side may counter, so this is the same request whichever way the negotiation is
/// travelling. Whose turn it is, is the aggregate's answer rather than the caller's claim.
/// </para>
/// </summary>
public sealed record CounterOfferRequest
{
    public required decimal Amount { get; init; }

    /// <summary>How many units. Carried over from the standing terms when omitted.</summary>
    public int? Quantity { get; init; }

    public string? Message { get; init; }

    /// <summary>When this counter lapses. The deployment default applies when omitted.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
