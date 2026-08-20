using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;

namespace Diaspora.Modules.Financial.Domain.Ledgers;

/// <summary>
/// What a statement request asks for.
/// <para>
/// Paged by <see cref="AfterSequence"/> rather than by a timestamp, and that is the whole reason the
/// sequence exists. Two entries in one transaction share a timestamp to the microsecond; a
/// timestamp-only cursor silently drops one of them, which the platform recorded as the worst
/// property a paging bug can have. A gapless per-wallet sequence makes the keyset exact.
/// </para>
/// </summary>
public sealed record LedgerQuery
{
    public required WalletId WalletId { get; init; }

    /// <summary>Return entries strictly older than this sequence. Null starts at the newest.</summary>
    public long? AfterSequence { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? To { get; init; }

    public LedgerEntryType? EntryType { get; init; }

    public LedgerDirection? Direction { get; init; }

    public LedgerBucket? Bucket { get; init; }

    public int PageSize { get; init; } = Policies.FinancialLimits.DefaultLedgerPageSize;
}
