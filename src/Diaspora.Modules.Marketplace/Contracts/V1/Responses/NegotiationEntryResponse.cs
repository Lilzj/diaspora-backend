namespace Diaspora.Modules.Marketplace.Contracts.V1.Responses;

/// <summary>One move in a negotiation, as the history renders it.</summary>
public sealed record NegotiationEntryResponse
{
    public required int Round { get; init; }

    /// <summary>
    /// <c>Offered</c>, <c>Countered</c>, <c>Accepted</c>, <c>Rejected</c>, <c>Withdrawn</c> or
    /// <c>Expired</c>.
    /// </summary>
    public required string Action { get; init; }

    /// <summary><c>Buyer</c> or <c>Seller</c>. Always known, even when the actor is not.</summary>
    public required string Party { get; init; }

    /// <summary>What was on the table after this move. Null on a rejection or a withdrawal.</summary>
    public decimal? Amount { get; init; }

    public required string Currency { get; init; }

    public required int Quantity { get; init; }

    /// <summary>What the party said. Plain text.</summary>
    public string? Message { get; init; }

    public required DateTimeOffset OccurredAt { get; init; }
}
