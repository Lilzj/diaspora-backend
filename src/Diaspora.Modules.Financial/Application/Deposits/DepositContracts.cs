using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Deposits;

/// <summary>
/// Adds money to the caller's own balance.
/// <para>
/// <b>A payment intent with a purpose, not a new kind of money movement.</b> B15 already models
/// money arriving from outside; what it did not model was money arriving from outside and landing in
/// the payer's own wallet rather than the platform's revenue. That is one enum member on the intent
/// and a different credit leg at capture — everything else, the gateway, the state machine, the
/// idempotency key, the webhook confirmation, is the path that already existed.
/// </para>
/// <para>
/// <b>Nothing here names a wallet.</b> The credit goes to the primary wallet on the account the
/// intent is opened against, resolved server-side. There is no path from a request body to a
/// balance that goes up.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.PaymentsManage)]
[Transactional(Reason = "A first deposit opens the payer's financial identity alongside the intent.")]
[Auditable(Action = "financial.deposit.create")]
public sealed record CreateDepositCommand : ICommand<PaymentIntentResponse>
{
    public required decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string MethodKind { get; init; }

    public string? CountryCode { get; init; }

    public string? IdempotencyKey { get; init; }
}

/// <summary>
/// The caller's top-ups, distinct from their payments.
/// <para>
/// Distinct because the purpose is stored on the intent, so this is a filter rather than a second
/// table — which is what "do not rebuild B15" looks like when the thing being asked for is genuinely
/// a view of something that exists.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.WalletRead)]
public sealed record GetMyDepositsQuery : IQuery<IReadOnlyList<PaymentIntentResponse>>
{
    public string? Status { get; init; }

    /// <summary>Return deposits created at or before this instant. See ADR-017.</summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>Identifiers already served at the boundary instant.</summary>
    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}

/// <summary>
/// What the caller may deposit, and what it would cost them.
/// <para>
/// Read before the amount screen so the client never invents a minimum, a maximum or a daily cap.
/// Every figure comes from a versioned policy row.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.WalletRead)]
public sealed record GetDepositLimitsQuery : IQuery<MovementLimitsResponse>
{
    public string? Currency { get; init; }

    public string? CountryCode { get; init; }
}
