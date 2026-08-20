using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Withdrawals;

/// <summary>
/// Takes money off the platform.
/// <para>
/// <b>Transactional, because the request and the hold behind it must land together.</b> A withdrawal
/// row with no hold is money a member can spend twice; a hold with no withdrawal is money nobody can
/// reach. The unit of work is what makes neither possible.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Transactional(Reason = "The request and the ledger hold behind it land together or not at all.")]
[Auditable(Action = "financial.withdrawal.create")]
public sealed record CreateWithdrawalCommand : ICommand<WithdrawalResponse>
{
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required Guid DestinationId { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>Calls off a withdrawal nobody has picked up yet, and releases the hold.</summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Transactional(Reason = "Cancelling the request and releasing its hold are one change.")]
[Auditable(Action = "financial.withdrawal.cancel")]
public sealed record CancelWithdrawalCommand : ICommand<WithdrawalResponse>
{
    public required Guid WithdrawalId { get; init; }
}

/// <summary>The caller's payout requests.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetMyWithdrawalsQuery : IQuery<IReadOnlyList<WithdrawalResponse>>
{
    public string? Status { get; init; }

    public DateTimeOffset? Before { get; init; }

    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}

/// <summary>One payout request of the caller's.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetWithdrawalQuery : IQuery<WithdrawalResponse>
{
    public required Guid WithdrawalId { get; init; }
}

/// <summary>What the caller may withdraw, what it would cost, and what they hold.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetWithdrawalLimitsQuery : IQuery<MovementLimitsResponse>
{
    public string? Currency { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>
/// Registers somewhere money can be sent.
/// <para>
/// The request carries a label and a provider token, never a credential — see
/// <c>RegisterPayoutDestinationRequest</c>. This module could not store one if it were sent.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Auditable(Action = "financial.payout_destination.register")]
public sealed record RegisterPayoutDestinationCommand : ICommand<PayoutDestinationResponse>
{
    public required string Kind { get; init; }

    public required string Label { get; init; }

    public required string Currency { get; init; }

    public string? Provider { get; init; }

    public string? ProviderToken { get; init; }

    public string? CountryCode { get; init; }
}

/// <summary>The caller's payout destinations.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetMyPayoutDestinationsQuery : IQuery<IReadOnlyList<PayoutDestinationResponse>>;

/// <summary>Stops using a destination. Never deletes it — old withdrawals still name it.</summary>
[Authorize(Permission = FinancialPermissions.PaymentsManage)]
[Auditable(Action = "financial.payout_destination.remove")]
public sealed record RemovePayoutDestinationCommand : ICommand<PayoutDestinationResponse>
{
    public required Guid DestinationId { get; init; }
}

/// <summary>
/// Marks a destination verified. Staff.
/// <para>
/// Verification itself is the settlement provider's — a micro deposit, a name match, an open-banking
/// handshake. B15 records only that somebody who could check did, and refuses to pay out until they
/// have.
/// </para>
/// </summary>
[Authorize(Permission = FinancialPermissions.WalletManage)]
[Auditable(Action = "financial.payout_destination.verify")]
public sealed record VerifyPayoutDestinationCommand : ICommand<PayoutDestinationResponse>
{
    public required Guid DestinationId { get; init; }

    public string? Provider { get; init; }

    public string? ProviderToken { get; init; }
}
