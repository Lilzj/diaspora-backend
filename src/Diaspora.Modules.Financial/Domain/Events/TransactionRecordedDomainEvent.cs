using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Events;

/// <summary>
/// Money moved.
/// <para>
/// <b>The transaction is announced, never the individual entries.</b> A single purchase produces up
/// to six ledger entries — a debit, a credit, a commission leg, a fee leg and both sides of the
/// platform's clearing — and publishing each would put six messages on the bus for one fact and
/// leave every consumer reassembling them. The transaction is the unit that means something.
/// </para>
/// </summary>
public sealed record TransactionRecordedDomainEvent : DomainEvent
{
    public required TransactionId TransactionId { get; init; }

    public required TransactionType TransactionType { get; init; }

    public required TransactionStatus Status { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required int EntryCount { get; init; }

    public Guid? InitiatedByUserId { get; init; }

    public MonetizationSubjectType? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public string? Reference { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }
}
