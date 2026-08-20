namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Grant platform credit. Staff only.
/// <para>
/// The reason is required and is written onto the ledger entry, because a credit nobody can explain
/// is indistinguishable from one somebody granted themselves.
/// </para>
/// </summary>
public sealed record GrantCreditRequest
{
    public required Guid UserId { get; init; }

    /// <summary>Major units.</summary>
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    /// <summary>
    /// <c>Promotional</c>, <c>Compensation</c>, <c>Referral</c>, <c>RefundToCredit</c>,
    /// <c>Bonus</c> or <c>Purchased</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>Why. Written onto the ledger entry and visible to the member.</summary>
    public required string Reason { get; init; }

    /// <summary>Null when the credit never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
