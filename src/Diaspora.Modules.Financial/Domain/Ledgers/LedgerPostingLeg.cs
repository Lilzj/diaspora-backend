using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;

namespace Diaspora.Modules.Financial.Domain.Ledgers;

/// <summary>
/// One side of a posting: this wallet, this bucket, this direction, this amount.
/// <para>
/// A request rather than an entry. The entry does not exist until the posting service has loaded the
/// wallet, taken the next sequence and computed the balance — which is why a caller cannot construct
/// a <see cref="LedgerEntry"/> with a balance of its own choosing.
/// </para>
/// </summary>
public sealed record LedgerPostingLeg
{
    public required WalletId WalletId { get; init; }

    public required LedgerDirection Direction { get; init; }

    public required Money Amount { get; init; }

    public LedgerBucket Bucket { get; init; } = LedgerBucket.Available;

    public required LedgerEntryType EntryType { get; init; }

    /// <summary>What this leg is about, when it differs from the transaction's own subject.</summary>
    public SubjectReference? Subject { get; init; }

    public string? ExternalReference { get; init; }
}
