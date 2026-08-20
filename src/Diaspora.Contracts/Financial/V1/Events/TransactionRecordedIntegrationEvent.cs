using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Financial.V1.Events;

/// <summary>
/// Money moved.
/// <para>
/// <b>The transaction is announced; the individual ledger entries are not.</b> A single purchase
/// produces up to six entries — a debit, a credit, a commission leg, a fee leg and both sides of the
/// platform's clearing — and publishing each would put six messages on the bus for one fact and
/// leave every consumer reassembling them.
/// </para>
/// </summary>
public sealed record TransactionRecordedIntegrationEvent : IntegrationEvent
{
    public required Guid TransactionId { get; init; }

    /// <summary><c>Payment</c>, <c>Refund</c>, <c>Credit</c>, <c>Donation</c>, and so on.</summary>
    public required string TransactionType { get; init; }

    public required string Status { get; init; }

    public required long AmountMinorUnits { get; init; }

    public required string Currency { get; init; }

    public required int EntryCount { get; init; }

    public Guid? InitiatedByUserId { get; init; }

    public string? SubjectType { get; init; }

    public Guid? SubjectId { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }
}
