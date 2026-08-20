using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Payments;

/// <summary>
/// One refund against a payment intent.
/// <para>
/// Several are possible: a partial refund today and the rest next week is an ordinary sequence, and
/// modelling a refund as a single nullable amount on the intent would make the second one
/// unrepresentable and the first one unattributable.
/// </para>
/// <para>Owned by the intent, caller-supplied key, <c>ValueGeneratedNever()</c>.</para>
/// </summary>
public sealed class RefundRecord : ValueObject
{
    private RefundRecord(
        Guid refundId,
        long amountMinorUnits,
        RefundReason reason,
        RefundStatus status,
        string? note,
        Guid? issuedByUserId,
        string? externalReference,
        DateTimeOffset issuedAt)
    {
        RefundId = refundId;
        AmountMinorUnits = amountMinorUnits;
        Reason = reason;
        Status = status;
        Note = note;
        IssuedByUserId = issuedByUserId;
        ExternalReference = externalReference;
        IssuedAt = issuedAt;
    }

    private RefundRecord() { }

    public Guid RefundId { get; private set; }

    public long AmountMinorUnits { get; private set; }

    public RefundReason Reason { get; private set; }

    public RefundStatus Status { get; private set; }

    public string? Note { get; private set; }

    /// <summary>Null when a policy issued it rather than a person.</summary>
    public Guid? IssuedByUserId { get; private set; }

    public string? ExternalReference { get; private set; }

    public DateTimeOffset IssuedAt { get; private set; }

    public static RefundRecord Create(
        Guid refundId,
        long amountMinorUnits,
        RefundReason reason,
        DateTimeOffset issuedAt,
        RefundStatus status = RefundStatus.Succeeded,
        string? note = null,
        Guid? issuedByUserId = null,
        string? externalReference = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amountMinorUnits);

        return new RefundRecord(
            refundId,
            amountMinorUnits,
            reason,
            status,
            string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            issuedByUserId,
            string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim(),
            issuedAt);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RefundId;
        yield return AmountMinorUnits;
        yield return Reason;
        yield return Status;
        yield return Note;
        yield return IssuedByUserId;
        yield return ExternalReference;
        yield return IssuedAt;
    }
}
