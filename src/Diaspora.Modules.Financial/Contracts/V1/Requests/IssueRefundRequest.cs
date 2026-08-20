namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>Give money back, in whole or in part.</summary>
public sealed record IssueRefundRequest
{
    /// <summary>Major units. Null refunds everything still refundable.</summary>
    public decimal? Amount { get; init; }

    /// <summary>
    /// <c>RequestedByPayer</c>, <c>Duplicate</c>, <c>Fraudulent</c>, <c>NotDelivered</c>,
    /// <c>PolicyRule</c> or <c>Goodwill</c>.
    /// </summary>
    public required string Reason { get; init; }

    public string? Note { get; init; }
}
