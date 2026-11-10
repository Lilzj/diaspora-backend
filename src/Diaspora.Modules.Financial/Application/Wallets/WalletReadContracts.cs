using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Contracts.V1.Responses;

namespace Diaspora.Modules.Financial.Application.Wallets;

/// <summary>
/// One transaction of the caller's, with the entries that made it up.
/// <para>
/// <b>The route a transaction older than one page had no way of being reached by.</b> B15 served the
/// list and the statement and nothing resolved a transaction by identifier, so a client holding one —
/// from a notification, a receipt, a support conversation — had to page until it found it or give up.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.WalletRead)]
public sealed record GetTransactionQuery : IQuery<TransactionDetailResponse>
{
    public required Guid TransactionId { get; init; }
}

/// <summary>
/// A financial receipt, by its reference alone.
/// <para>
/// <b>Deliberately not scoped to the caller.</b> An anonymous gift stores no donor identifier, so it
/// is in nobody's history and the reference is the only handle the giver was ever issued — a route
/// that required ownership would make the receipt notification for an anonymous donation lead to a
/// screen that could not find it.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.GivingRead)]
public sealed record GetReceiptQuery : IQuery<ReceiptResponse>
{
    public required string ReceiptReference { get; init; }
}

/// <summary>
/// Money in and money out over a period, summed by the server.
/// <para>
/// Exists because the alternative was paging a year of ledger entries to a phone and summing them
/// there — which works for a light user and is quietly wrong for a heavy one.
/// </para>
/// </summary>
[RequirePermission(FinancialPermissions.WalletRead)]
public sealed record GetWalletAnalyticsQuery : IQuery<WalletAnalyticsResponse>
{
    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }
}
