using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Payments;

/// <summary>The caller's payments.</summary>
[RequirePermission(FinancialPermissions.PaymentsRead)]
public sealed record GetPaymentHistoryQuery : IQuery<IReadOnlyList<PaymentIntentResponse>>
{
    public string? Status { get; init; }

    /// <summary>Return payments created at or before this instant. See ADR-017.</summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>Identifiers already served at the boundary instant.</summary>
    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int Take { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}
