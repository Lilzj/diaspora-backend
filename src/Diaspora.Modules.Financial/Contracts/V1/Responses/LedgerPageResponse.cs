namespace Diaspora.Modules.Financial.Contracts.V1.Responses;

/// <summary>
/// A page of a statement.
/// <para>
/// The cursor is the sequence number of the oldest entry on the page. Paging by sequence rather than
/// by timestamp is what stops entries sharing a transaction's timestamp being silently dropped — the
/// worst property a paging bug can have, and one the platform has on record.
/// </para>
/// </summary>
public sealed record LedgerPageResponse
{
    public required Guid WalletId { get; init; }

    public required IReadOnlyList<LedgerEntryResponse> Entries { get; init; }

    /// <summary>Pass as <c>afterSequence</c> for the next page. Null when there are no more.</summary>
    public long? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
