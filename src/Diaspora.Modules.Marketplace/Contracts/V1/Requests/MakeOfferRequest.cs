namespace Diaspora.Modules.Marketplace.Contracts.V1.Requests;

/// <summary>Opens a negotiation on a listing.</summary>
public sealed record MakeOfferRequest
{
    /// <summary>The total offered, for the whole quantity.</summary>
    public required decimal Amount { get; init; }

    /// <summary>
    /// ISO 4217. Must match the listing's — nothing in this module converts, and comparing two
    /// numbers in different units is worse than refusing.
    /// </summary>
    public string? Currency { get; init; }

    /// <summary>How many units. One when omitted.</summary>
    public int? Quantity { get; init; }

    /// <summary>What the buyer wants to say with it. Plain text.</summary>
    public string? Message { get; init; }

    /// <summary>
    /// When the offer lapses if nobody answers. The deployment default applies when omitted, so a
    /// seller is never left with an open commitment they forgot about.
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
