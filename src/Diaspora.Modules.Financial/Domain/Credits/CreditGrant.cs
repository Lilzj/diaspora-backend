using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Financial.Domain.Credits;

/// <summary>
/// Platform credit granted to a wallet.
/// <para>
/// <b>Credit is not money and the platform must never confuse the two.</b> It is granted rather than
/// deposited, it can expire, it cannot be withdrawn, and it lives in a <c>Promotional</c> wallet
/// precisely so that "what do we owe people" and "what have we given people" are two numbers rather
/// than one. A platform that credited goodwill into a withdrawable balance has issued currency.
/// </para>
/// <para>
/// <b>The grant is a record; the ledger is the truth.</b> This row says who granted what and why;
/// the balance it produced is a ledger entry like any other. Revoking a grant appends a compensating
/// entry — it does not edit this row's amount, and it cannot take back credit already spent.
/// </para>
/// </summary>
public sealed class CreditGrant : AggregateRoot<CreditGrantId>, IAuditableEntity
{
    private CreditGrant(
        CreditGrantId id,
        WalletId walletId,
        Guid ownerId,
        CreditKind kind,
        Money amount,
        string reason,
        DateTimeOffset grantedAt) : base(id)
    {
        WalletId = walletId;
        OwnerId = ownerId;
        Kind = kind;
        AmountMinorUnits = amount.MinorUnits;
        Currency = amount.Currency;
        Exponent = amount.Exponent;
        Reason = reason;
        GrantedAt = grantedAt;
        CreatedAt = grantedAt;
    }

    private CreditGrant() { }

    public WalletId WalletId { get; private set; } = default!;

    public Guid OwnerId { get; private set; }

    public CreditKind Kind { get; private set; }

    public long AmountMinorUnits { get; private set; }

    public string Currency { get; private set; } = string.Empty;

    public int Exponent { get; private set; } = Money.DefaultExponent;

    public string Reason { get; private set; } = string.Empty;

    public Guid? GrantedByUserId { get; private set; }

    /// <summary>Null when the credit never expires.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public bool IsRevoked { get; private set; }

    public bool IsExpired { get; private set; }

    public DateTimeOffset GrantedAt { get; private set; }

    public TransactionId? TransactionId { get; private set; }

    public PromotionId? PromotionId { get; private set; }

    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public Money Amount => Money.FromMinorUnits(AmountMinorUnits, Currency, Exponent);

    public bool IsLive => !IsRevoked && !IsExpired;

    public static CreditGrant Issue(
        CreditGrantId id,
        WalletId walletId,
        Guid ownerId,
        FinancialAccountType ownerType,
        CreditKind kind,
        Money amount,
        string reason,
        DateTimeOffset grantedAt,
        Guid? grantedByUserId = null,
        DateTimeOffset? expiresAt = null,
        PromotionId? promotionId = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(walletId);
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (amount.IsZero)
        {
            throw new DomainException(
                "A credit grant of nothing is not a grant.", FinancialErrorCodes.AmountInvalid);
        }

        var grant = new CreditGrant(
            id, walletId, ownerId, kind, amount, reason.Trim(), grantedAt)
        {
            GrantedByUserId = grantedByUserId,
            ExpiresAt = expiresAt,
            PromotionId = promotionId
        };

        grant.RaiseDomainEvent(new CreditGrantedDomainEvent
        {
            GrantId = id,
            WalletId = walletId,
            OwnerId = ownerId,
            OwnerType = ownerType,
            Kind = kind,
            AmountMinorUnits = amount.MinorUnits,
            Currency = amount.Currency,
            Reason = grant.Reason,
            ExpiresAt = expiresAt,
            GrantedAt = grantedAt
        });

        return grant;
    }

    public void AttachTransaction(TransactionId transactionId)
    {
        ArgumentNullException.ThrowIfNull(transactionId);

        TransactionId = transactionId;
        Revision++;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (IsRevoked)
        {
            return;
        }

        IsRevoked = true;
        Revision++;
        ModifiedAt = now;
    }

    public void MarkExpired(DateTimeOffset now)
    {
        if (IsExpired)
        {
            return;
        }

        IsExpired = true;
        Revision++;
        ModifiedAt = now;
    }

    public bool HasLapsed(DateTimeOffset moment) =>
        IsLive && ExpiresAt is { } expiry && moment >= expiry;

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
