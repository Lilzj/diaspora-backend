namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// One transaction with its ledger entries inline.
/// <para>
/// <b>The entries travel with the transaction because the alternative was walking the statement.</b>
/// A client that had a transaction identifier and wanted to know what actually moved had to page a
/// wallet's statement looking for entries carrying it, and give up after a few pages. Two facts that
/// are always wanted together belong in one response.
/// </para>
/// </summary>
public sealed record TransactionDetailResponse
{
    public required TransactionResponse Transaction { get; init; }

    /// <summary>
    /// The entries this transaction wrote against wallets the caller can see.
    /// <para>
    /// A capture writes two entries: one against the platform's clearing wallet and one against
    /// somebody's. A member sees the leg that is theirs; staff with <c>financial:wallet:read:any</c>
    /// see both. Serving the platform's own legs to a member would put the platform's clearing
    /// position on a phone.
    /// </para>
    /// </summary>
    public required IReadOnlyList<LedgerEntryResponse> Entries { get; init; }
}
