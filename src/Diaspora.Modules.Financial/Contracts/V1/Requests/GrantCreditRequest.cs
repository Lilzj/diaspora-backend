namespace Diaspora.Modules.Financial.Contracts.V1.Requests;

/// <summary>
/// Grant platform credit. Staff only.
/// <para>
/// <b>Bounded since 7 Nov 2026.</b> Not to yourself; a reason of at least 10 characters; at most 100
/// of the currency's major unit; above 50, the direct grant is refused and the same body goes to
/// <c>POST /financial/operations/credit/grant-requests</c>, which a second Admin decides.
/// </para>
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

    /// <summary>Why. At least 10 characters. Written onto the ledger entry and visible to the member.</summary>
    public required string Reason { get; init; }

    /// <summary>Null when the credit never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }
}
