using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Transactions;

/// <summary>
/// The unit of work in the ledger: a set of entries that balances to zero.
/// <para>
/// <b>Every transaction on this platform balances, including the ones that come from outside it.</b>
/// A card capture credits a member and debits the platform's clearing wallet; a payout does the
/// reverse. Without a clearing counterparty, money arriving from a gateway would be a credit with no
/// matching debit — a ledger that does not balance, which is a ledger that cannot be checked, which
/// is a ledger that is not worth keeping.
/// </para>
/// <para>
/// <b>The transaction is what is announced; the entries are not.</b> One purchase can produce six
/// entries, and publishing each would put six messages on the bus for one fact and leave every
/// consumer reassembling them.
/// </para>
/// <para>
/// <b>Reversal appends.</b> <see cref="MarkReversed"/> records which transaction undid this one; the
/// original entries are untouched and the compensating entries are their own transaction. There is
/// no path that removes anything.
/// </para>
/// </summary>
public sealed class FinancialTransaction : AggregateRoot<TransactionId>, IAuditableEntity
{
    private FinancialTransaction(
        TransactionId id,
        TransactionType transactionType,
        Money amount,
        string description,
        AuditStamp audit,
        DateTimeOffset occurredAt) : base(id)
    {
        TransactionType = transactionType;
        AmountMinorUnits = amount.MinorUnits;
        Currency = amount.Currency;
        Exponent = amount.Exponent;
        Description = description;
        Audit = audit;
        OccurredAt = occurredAt;
        Status = TransactionStatus.Pending;
        CreatedAt = occurredAt;
    }

    private FinancialTransaction() { }

    public TransactionType TransactionType { get; private set; }

    public TransactionStatus Status { get; private set; }

    /// <summary>
    /// The headline figure — what a member sees on a statement line. Not the sum of the entries,
    /// which is zero by construction.
    /// </summary>
    public long AmountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    public int EntryCount { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public AuditStamp Audit { get; private set; } = default!;

    /// <summary>What this was about. Never resolved here.</summary>
    public MonetizationSubjectType? SubjectType { get; private set; }

    public Guid? SubjectId { get; private set; }

    /// <summary>The subject as a value object, rebuilt on read. See <c>LedgerEntry.Subject</c>.</summary>
    public SubjectReference? Subject =>
        SubjectType is { } type && SubjectId is { } id
            ? SubjectReference.Create(type, id)
            : null;

    public Guid? InitiatedByUserId { get; private set; }

    /// <summary>
    /// The caller's idempotency key, or a gateway reference. Unique when present — replaying a
    /// request must not move money twice, and the database is what enforces that rather than a
    /// check the handler could be refactored past.
    /// </summary>
    public string? Reference { get; private set; }

    public string? CountryCode { get; private set; }

    /// <summary>Set on the compensating transaction, naming what it undid.</summary>
    public TransactionId? ReversalOfTransactionId { get; private set; }

    /// <summary>Set on the original, naming what undid it.</summary>
    public TransactionId? ReversedByTransactionId { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency, Exponent);

    public static FinancialTransaction Begin(
        TransactionId id,
        TransactionType transactionType,
        Money amount,
        string description,
        AuditStamp audit,
        DateTimeOffset occurredAt,
        SubjectReference? subject = null,
        Guid? initiatedByUserId = null,
        string? reference = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new FinancialTransaction(id, transactionType, amount, description.Trim(), audit, occurredAt)
        {
            SubjectType = subject?.SubjectType,
            SubjectId = subject?.SubjectId,
            InitiatedByUserId = initiatedByUserId,
            Reference = string.IsNullOrWhiteSpace(reference) ? null : reference.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode)
                ? null
                : countryCode.Trim().ToUpperInvariant()
        };
    }

    public void Complete(int entryCount, DateTimeOffset now)
    {
        if (Status != TransactionStatus.Pending)
        {
            throw new DomainException(
                $"A {Status} transaction cannot be completed.",
                FinancialErrorCodes.TransactionNotPending);
        }

        Status = TransactionStatus.Completed;
        EntryCount = entryCount;
        CompletedAt = now;
        Revision++;

        RaiseDomainEvent(new TransactionRecordedDomainEvent
        {
            TransactionId = Id,
            TransactionType = TransactionType,
            Status = Status,
            AmountMinorUnits = AmountMinorUnits,
            Currency = Currency,
            EntryCount = entryCount,
            InitiatedByUserId = InitiatedByUserId,
            SubjectType = Subject?.SubjectType,
            SubjectId = Subject?.SubjectId,
            Reference = Reference,
            RecordedAt = now
        });
    }

    public void Fail(string reason, DateTimeOffset now)
    {
        if (Status is TransactionStatus.Completed or TransactionStatus.Reversed)
        {
            throw new DomainException(
                $"A {Status} transaction cannot be failed.",
                FinancialErrorCodes.TransactionNotPending);
        }

        Status = TransactionStatus.Failed;
        FailureReason = string.IsNullOrWhiteSpace(reason) ? "Failed." : reason.Trim();
        CompletedAt = now;
        Revision++;
    }

    /// <summary>Records that a compensating transaction has undone this one.</summary>
    public void MarkReversed(TransactionId reversedBy, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(reversedBy);

        if (Status != TransactionStatus.Completed)
        {
            throw new DomainException(
                "Only a completed transaction can be reversed.",
                FinancialErrorCodes.TransactionNotReversible);
        }

        Status = TransactionStatus.Reversed;
        ReversedByTransactionId = reversedBy;
        Revision++;
        ModifiedAt = now;
    }

    /// <summary>Records that this transaction is itself the reversal of another.</summary>
    public void MarkAsReversalOf(TransactionId original)
    {
        ArgumentNullException.ThrowIfNull(original);

        ReversalOfTransactionId = original;
    }

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
