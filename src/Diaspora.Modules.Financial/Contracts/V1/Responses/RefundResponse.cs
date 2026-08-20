namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>One refund against a payment.</summary>
public sealed record RefundResponse
{
    public required Guid RefundId { get; init; }

    public required MoneyResponse Amount { get; init; }

    /// <summary>
    /// <c>RequestedByPayer</c>, <c>Duplicate</c>, <c>Fraudulent</c>, <c>NotDelivered</c>,
    /// <c>PolicyRule</c> or <c>Goodwill</c>.
    /// </summary>
    public required string Reason { get; init; }

    public required string Status { get; init; }

    public string? Note { get; init; }

    public required DateTimeOffset IssuedAt { get; init; }
}
