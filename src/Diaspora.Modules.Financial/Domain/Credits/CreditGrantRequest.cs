using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Credits;

/// <summary>
/// A promotional credit grant above the one-Admin threshold, waiting for a second Admin (7 Nov 2026).
/// <para>
/// <b>Asking grants nothing.</b> The credit lands when a different Admin approves, through the same
/// issuer as a direct grant, naming both hands. That is why the request carries everything the grant
/// needs. It copies the reward-points <c>RewardGrantRequest</c> (RWD-10), which settled the shape.
/// </para>
/// </summary>
public sealed class CreditGrantRequest : AggregateRoot<CreditGrantRequestId>, IAuditableEntity
{
    private CreditGrantRequest(
        CreditGrantRequestId id,
        Guid memberUserId,
        Money amount,
        CreditKind kind,
        string reason,
        DateTimeOffset? expiresAt,
        Guid requestedByUserId,
        DateTimeOffset requestedAt) : base(id)
    {
        MemberUserId = memberUserId;
        AmountMinorUnits = amount.MinorUnits;
        Currency = amount.Currency;
        Exponent = amount.Exponent;
        Kind = kind;
        Reason = reason;
        ExpiresAt = expiresAt;
        RequestedByUserId = requestedByUserId;
        RequestedAt = requestedAt;
        CreatedAt = requestedAt;
        Status = CreditGrantRequestStatus.Pending;
    }

    private CreditGrantRequest() { }

    /// <summary>Who the credit is for.</summary>
    public Guid MemberUserId { get; private set; }

    public long AmountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    public CreditKind Kind { get; private set; }

    /// <summary>Why the member is being given this credit. The member reads it.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>When the credit, once granted, expires. Null when it never does.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>The Admin who asked. The grant is theirs.</summary>
    public Guid RequestedByUserId { get; private set; }

    public DateTimeOffset RequestedAt { get; private set; }

    public CreditGrantRequestStatus Status { get; private set; }

    /// <summary>Who approved or rejected it. Null while pending, and on a withdrawal.</summary>
    public Guid? DecidedByUserId { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Why it was declined or withdrawn, or anything the approver added.</summary>
    public string? DecisionNote { get; private set; }

    /// <summary>The grant an approval produced.</summary>
    public CreditGrantId? CreditGrantId { get; private set; }

    public bool IsPending => Status == CreditGrantRequestStatus.Pending;

    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency, Exponent);

    /// <summary>Bumped on every change. Two Admins deciding at once: one wins, one gets 409.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreditGrantRequest Raise(
        CreditGrantRequestId id,
        Guid memberUserId,
        Money amount,
        CreditKind kind,
        string reason,
        DateTimeOffset? expiresAt,
        Guid requestedByUserId,
        DateTimeOffset requestedAt)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (amount.IsZero)
        {
            throw new DomainException(
                "A credit grant of nothing is not a grant.", FinancialErrorCodes.AmountInvalid);
        }

        return new CreditGrantRequest(
            id, memberUserId, amount, kind, reason.Trim(), expiresAt, requestedByUserId, requestedAt);
    }

    /// <summary>A second Admin agreed. The caller issues the grant in the same unit of work.</summary>
    public void Approve(Guid approvedByUserId, string? note, CreditGrantId grantId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grantId);

        RequirePending();

        Status = CreditGrantRequestStatus.Approved;
        CreditGrantId = grantId;
        Settle(approvedByUserId, note, now);
    }

    public void Reject(Guid decidedByUserId, string note, DateTimeOffset now)
    {
        RequirePending();

        Status = CreditGrantRequestStatus.Rejected;
        Settle(decidedByUserId, note, now);
    }

    /// <summary>The Admin who asked took it back. Nobody else decided, so nobody is named.</summary>
    public void Withdraw(string note, DateTimeOffset now)
    {
        RequirePending();

        Status = CreditGrantRequestStatus.Withdrawn;
        Settle(decidedByUserId: null, note, now);
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

    private void RequirePending()
    {
        if (!IsPending)
        {
            throw new DomainException(
                $"A credit grant request that is {Status} has already been decided.",
                FinancialErrorCodes.CreditGrantRequestDecided);
        }
    }

    private void Settle(Guid? decidedByUserId, string? note, DateTimeOffset now)
    {
        DecidedByUserId = decidedByUserId;
        DecisionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        DecidedAt = now;
        Revision++;
        ModifiedAt = now;
    }
}
