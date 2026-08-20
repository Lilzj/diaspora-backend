using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Payments;

/// <summary>
/// A member asking for their money back.
/// <para>
/// <b>A request is not a refund, and the whole reason this aggregate exists is to keep them
/// apart.</b> B15 shipped with a staff-only refund and no way for a member to ask for one; the
/// obvious fix — letting a member call the refund route — would have made the platform's decision
/// about whether something was delivered into the member's decision. This is the missing half: the
/// member states a case, a staff member answers, and only an approval reaches the money.
/// </para>
/// <para>
/// <b>An approval does not move money either.</b> The refund itself is a separate, later fact, because
/// a provider can still refuse a refund the platform has agreed to — and a member told "approved" who
/// never sees the money needs the platform to know which of those two things it is missing.
/// </para>
/// <para>
/// <b>One open request per payment.</b> Enforced by a filtered unique index rather than by a
/// handler check, so a double-tapped button produces one request and a second attempt while the first
/// is open is refused at the database.
/// </para>
/// </summary>
public sealed class RefundRequest : AggregateRoot<RefundRequestId>, IAuditableEntity
{
    private RefundRequest(
        RefundRequestId id,
        PaymentIntentId paymentIntentId,
        Guid requestedByUserId,
        Money amount,
        RefundReason reason,
        string note,
        DateTimeOffset requestedAt) : base(id)
    {
        PaymentIntentId = paymentIntentId;
        RequestedByUserId = requestedByUserId;
        AmountMinorUnits = amount.MinorUnits;
        Currency = amount.Currency;
        Exponent = amount.Exponent;
        Reason = reason;
        Note = note;
        Status = RefundRequestStatus.Requested;
        OpenForPaymentIntentId = paymentIntentId.Value;
        RequestedAt = requestedAt;
        CreatedAt = requestedAt;
    }

    private RefundRequest() { }

    public PaymentIntentId PaymentIntentId { get; private set; } = default!;

    public Guid RequestedByUserId { get; private set; }

    public long AmountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    public RefundRequestStatus Status { get; private set; }

    public RefundReason Reason { get; private set; }

    /// <summary>What the member said. Shown to whoever decides, and to nobody else.</summary>
    public string Note { get; private set; } = string.Empty;

    /// <summary>
    /// The payment while this request is open, and null once it has been answered.
    /// <para>
    /// <b>A nullable copy of the payment id rather than a boolean, because that is what a
    /// provider-neutral unique index needs.</b> PostgreSQL and SQLite both treat nulls in a unique
    /// index as distinct, so "one open request per payment" is a plain unique index on this column —
    /// no filtered-index syntax, which differs by provider and would work in production and not in
    /// the SQLite the integration suite runs on.
    /// </para>
    /// </summary>
    public Guid? OpenForPaymentIntentId { get; private set; }

    /// <summary>Whether nobody has answered yet. Derived; the stored form is above.</summary>
    public bool IsOpen => OpenForPaymentIntentId is not null;

    public Guid? DecidedByUserId { get; private set; }

    /// <summary>A member-safe explanation. Never an internal note.</summary>
    public string? DecisionNote { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>The refund that resulted, once one was actually issued.</summary>
    public Guid? RefundId { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency, Exponent);

    public static RefundRequest Raise(
        RefundRequestId id,
        PaymentIntentId paymentIntentId,
        Guid requestedByUserId,
        Money amount,
        RefundReason reason,
        string note,
        DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(paymentIntentId);
        ArgumentNullException.ThrowIfNull(amount);

        if (amount.IsZero)
        {
            throw new DomainException(
                "A refund request for nothing is not a request.",
                FinancialErrorCodes.AmountInvalid);
        }

        return new RefundRequest(
            id,
            paymentIntentId,
            requestedByUserId,
            amount,
            reason,
            string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim(),
            requestedAt);
    }

    /// <summary>A staff member agreed. Money has still not moved.</summary>
    public void Approve(Guid decidedByUserId, string? decisionNote, DateTimeOffset now)
    {
        Require(RefundRequestStatus.Requested);

        Status = RefundRequestStatus.Approved;
        Settle(decidedByUserId, decisionNote, now);
    }

    public void Reject(Guid decidedByUserId, string? decisionNote, DateTimeOffset now)
    {
        Require(RefundRequestStatus.Requested);

        Status = RefundRequestStatus.Rejected;
        Settle(decidedByUserId, decisionNote, now);
    }

    /// <summary>The member changed their mind before anybody answered.</summary>
    public void Withdraw(DateTimeOffset now)
    {
        Require(RefundRequestStatus.Requested);

        Status = RefundRequestStatus.Withdrawn;
        Settle(decidedByUserId: null, decisionNote: null, now);
    }

    /// <summary>Records which refund an approval eventually produced.</summary>
    public void AttachRefund(Guid refundId, DateTimeOffset now)
    {
        RefundId = refundId;
        Revision++;
        ModifiedAt = now;
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

    private void Require(params RefundRequestStatus[] allowed)
    {
        if (Array.IndexOf(allowed, Status) < 0)
        {
            throw new DomainException(
                $"A refund request in '{Status}' has already been answered.",
                FinancialErrorCodes.RefundRequestNotDecidable);
        }
    }

    private void Settle(Guid? decidedByUserId, string? decisionNote, DateTimeOffset now)
    {
        DecidedByUserId = decidedByUserId;
        DecisionNote = string.IsNullOrWhiteSpace(decisionNote) ? null : decisionNote.Trim();
        DecidedAt = now;

        // Freeing the payment for another request is the point: a rejected request must not lock a
        // member out of asking again after they have sent the evidence they were asked for.
        OpenForPaymentIntentId = null;
        Revision++;
        ModifiedAt = now;
    }
}
