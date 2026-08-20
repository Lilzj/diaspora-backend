using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Ledgers;

/// <summary>
/// One line in the canonical financial record.
/// <para>
/// <b>Append-only, and the type is what enforces it.</b> There is one factory,
/// <see cref="Append"/>, and there is no other public member that assigns anything. No
/// <c>Update</c>, no <c>Correct</c>, no <c>Void</c>, no soft delete — the aggregate does not
/// implement <c>ISoftDeletable</c> and does not implement <c>IAuditableEntity</c>, because both of
/// those exist to record that something changed and nothing here ever does. An architecture test
/// asserts the absence, so a future author who adds a setter fails the build rather than a
/// reconciliation.
/// </para>
/// <para>
/// <b>Correcting the ledger means appending to it.</b> A mistaken entry is answered with a
/// compensating entry of the opposite direction, carrying its own reason and its own audit stamp.
/// The wrong number stays visible for ever, which is the entire value of the record: a ledger you
/// can edit is a ledger nobody can rely on, and the edit is invisible precisely when it matters.
/// </para>
/// <para>
/// <b><see cref="BalanceAfterMinorUnits"/> is stored, not derived.</b> A statement whose running
/// total is recalculated on read is a statement that changes when the code does. B12 learned the
/// same lesson about attendance: deriving a historical figure from current state produces a
/// plausible number and a wrong one.
/// </para>
/// <para>
/// <b><see cref="Sequence"/> is per wallet, gapless and assigned by the wallet.</b> It is the
/// tie-break the statement is ordered by, because two entries in one transaction share a timestamp
/// to the microsecond and a statement ordered by time alone shows them in whichever order the
/// database felt like.
/// </para>
/// </summary>
public sealed class LedgerEntry : AggregateRoot<LedgerEntryId>
{
    private LedgerEntry(
        LedgerEntryId id,
        WalletId walletId,
        TransactionId transactionId,
        long sequence,
        LedgerEntryType entryType,
        LedgerDirection direction,
        LedgerBucket bucket,
        Money amount,
        long balanceBefore,
        long balanceAfter,
        AuditStamp audit,
        DateTimeOffset occurredAt) : base(id)
    {
        WalletId = walletId;
        TransactionId = transactionId;
        Sequence = sequence;
        EntryType = entryType;
        Direction = direction;
        Bucket = bucket;
        AmountMinorUnits = amount.MinorUnits;
        Currency = amount.Currency;
        Exponent = amount.Exponent;
        BalanceBeforeMinorUnits = balanceBefore;
        BalanceAfterMinorUnits = balanceAfter;
        Audit = audit;
        OccurredAt = occurredAt;
    }

    private LedgerEntry() { }

    public WalletId WalletId { get; private set; } = default!;

    /// <summary>The transaction this entry belongs to. Every entry belongs to exactly one.</summary>
    public TransactionId TransactionId { get; private set; } = default!;

    /// <summary>Position in this wallet's statement. Gapless, starting at 1.</summary>
    public long Sequence { get; private set; }

    public LedgerEntryType EntryType { get; private set; }

    public LedgerDirection Direction { get; private set; }

    public LedgerBucket Bucket { get; private set; }

    public long AmountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    /// <summary>The bucket's balance immediately before this entry.</summary>
    public long BalanceBeforeMinorUnits { get; private set; }

    /// <summary>The bucket's balance immediately after. Stored — see the class summary.</summary>
    public long BalanceAfterMinorUnits { get; private set; }

    /// <summary>Actor, source, reason, correlation, request and the policy version in force.</summary>
    public AuditStamp Audit { get; private set; } = default!;

    /// <summary>What this was about, when it was about something. Never resolved here.</summary>
    public MonetizationSubjectType? SubjectType { get; private set; }

    /// <summary>The identifier in the owning module. Opaque here.</summary>
    public Guid? SubjectId { get; private set; }

    /// <summary>
    /// The subject as a value object, rebuilt on read.
    /// <para>
    /// Stored as two nullable columns rather than as an optional owned type. EF maps an optional
    /// owned reference by making every column nullable and then cannot distinguish "absent" from
    /// "all properties null" — a distinction that matters here, because most entries have no
    /// subject at all.
    /// </para>
    /// </summary>
    public SubjectReference? Subject =>
        SubjectType is { } type && SubjectId is { } id
            ? SubjectReference.Create(type, id)
            : null;

    /// <summary>A gateway reference, a receipt number, an idempotency key.</summary>
    public string? ExternalReference { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency, Exponent);

    /// <summary>The signed effect on the bucket. Positive for a credit, negative for a debit.</summary>
    public long SignedMinorUnits =>
        Direction == LedgerDirection.Credit ? AmountMinorUnits : -AmountMinorUnits;

    /// <summary>
    /// The only way an entry comes into existence.
    /// <para>
    /// Takes the balance before and computes the balance after, rather than accepting both: a caller
    /// supplying two numbers is a caller who can supply two numbers that disagree, and the ledger
    /// would then hold arithmetic nobody did.
    /// </para>
    /// </summary>
    public static LedgerEntry Append(
        LedgerEntryId id,
        WalletId walletId,
        TransactionId transactionId,
        long sequence,
        LedgerEntryType entryType,
        LedgerDirection direction,
        LedgerBucket bucket,
        Money amount,
        long balanceBefore,
        AuditStamp audit,
        DateTimeOffset occurredAt,
        SubjectReference? subject = null,
        string? externalReference = null,
        bool allowNegativeBalance = false)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(walletId);
        ArgumentNullException.ThrowIfNull(transactionId);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sequence);

        var delta = direction == LedgerDirection.Credit ? amount.MinorUnits : -amount.MinorUnits;
        var balanceAfter = balanceBefore + delta;

        // Every wallet on the platform holds a non-negative balance, with exactly one exception:
        // the platform's clearing wallet, which represents the boundary with the outside world.
        // Its balance is by construction the negative of everything the platform has taken in, and
        // in a double-entry system one nominal account must be allowed to run that way or nothing
        // balances. Wallet.AllowsNegativeBalance is the only thing that passes true here.
        if (balanceAfter < 0 && !allowNegativeBalance)
        {
            throw new InvalidOperationException(
                $"Appending this entry would take the {bucket} balance to {balanceAfter}. "
                + "A bucket balance is never negative; the caller must check first.");
        }

        return new LedgerEntry(
            id, walletId, transactionId, sequence, entryType, direction, bucket,
            amount, balanceBefore, balanceAfter, audit, occurredAt)
        {
            SubjectType = subject?.SubjectType,
            SubjectId = subject?.SubjectId,
            ExternalReference = string.IsNullOrWhiteSpace(externalReference)
                ? null
                : externalReference.Trim()
        };
    }
}
