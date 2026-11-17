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
/// A financial receipt, scoped to its owner where it has one.
/// <para>
/// <b>Scoped to the caller when a giver was stored, and unscoped when none was</b> — BBL-004. An
/// anonymous gift stores no donor identifier, so it is in nobody's history and the reference is the
/// only handle its giver was ever issued; there is nothing to compare a caller against and no way to
/// re-issue it, because the platform does not know who they are. Those receipts are therefore served
/// to whoever presents the reference, and theirs are minted at 130 bits for exactly that reason.
/// Every other receipt has an owner, and is now served to that owner alone.
/// </para>
/// <para>
/// <b>The justification this comment used to give was for a path that does not exist.</b> It said an
/// ownership check "would make the receipt notification for an anonymous donation lead to a screen
/// that could not find it" — but <c>DonationCompletedConsumer</c> sends an anonymous donation no
/// notification at all, and says why: the contract "carries no identifier to send it to". The read
/// path is still load-bearing for a different and better reason. An anonymous gift completes
/// asynchronously after its payment and appears in no history, so this route is the only way its
/// giver can ever learn whether the money arrived.
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
