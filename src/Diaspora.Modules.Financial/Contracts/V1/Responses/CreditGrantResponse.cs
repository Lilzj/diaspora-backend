namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>Platform credit that was granted.</summary>
public sealed record CreditGrantResponse
{
    public required Guid GrantId { get; init; }

    public required Guid WalletId { get; init; }

    /// <summary>
    /// <c>Promotional</c>, <c>Compensation</c>, <c>Referral</c>, <c>RefundToCredit</c>,
    /// <c>Bonus</c> or <c>Purchased</c>.
    /// </summary>
    public required string Kind { get; init; }

    public required MoneyResponse Amount { get; init; }

    public required string Reason { get; init; }

    /// <summary>Null when the credit never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public required bool IsLive { get; init; }

    public required DateTimeOffset GrantedAt { get; init; }
}
