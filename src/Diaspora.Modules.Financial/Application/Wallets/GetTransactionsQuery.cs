using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>The caller's transaction history.</summary>
[Authorize(Permission = FinancialPermissions.WalletRead)]
public sealed record GetTransactionsQuery : IQuery<IReadOnlyList<TransactionResponse>>
{
    public string? TransactionType { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    /// <summary>Return transactions at or before this instant.</summary>
    public DateTimeOffset? Before { get; init; }

    /// <summary>Identifiers already served at the boundary instant. See ADR-017.</summary>
    public IReadOnlyList<Guid> ExcludeIds { get; init; } = [];

    public int PageSize { get; init; } = Domain.Policies.FinancialLimits.DefaultLedgerPageSize;
}
